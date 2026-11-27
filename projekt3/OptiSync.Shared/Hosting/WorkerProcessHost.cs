using System.IO;
using System.IO.Pipes;
using OptiSync.Shared.Domain;
using OptiSync.Shared.Engine;
using OptiSync.Shared.Ipc;
using OptiSync.Shared.Parsing;
using OptiSync.Shared.Scripting;

namespace OptiSync.Shared.Hosting;

public static class WorkerProcessHost
{
    public const string WorkerSemaphoreName = @"Global\OptiSyncNP_WorkerProcessLimit";
    public const int WorkerProcessLimit = 2;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SemaphoreTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TelemetryInterval = TimeSpan.FromMilliseconds(250);

    public static async Task<int> RunAsync(string[] args, IWorkerEngine engine)
    {
        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Użycie: <worker>.exe <nazwa-potoku>");
            return 1;
        }

        string pipeName = args[0];
        Console.WriteLine($"[{engine.EngineName}] PID {Environment.ProcessId}, łączenie z potokiem '{pipeName}'...");

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using (var connectTimeout = new CancellationTokenSource(ConnectTimeout))
        {
            await pipe.ConnectAsync(connectTimeout.Token);
        }

        await using var channel = new AsyncPipeChannel(pipe);
        using var processLimit = new Semaphore(initialCount: WorkerProcessLimit, maximumCount: WorkerProcessLimit, name: WorkerSemaphoreName);

        if (!processLimit.WaitOne(SemaphoreTimeout))
        {
            string message = $"Osiągnięto limit {WorkerProcessLimit} aktywnych procesów roboczych (semafor {WorkerSemaphoreName}).";
            Console.Error.WriteLine(message);
            await RejectSessionAsync(channel, message);
            return 2;
        }

        try
        {
            Console.WriteLine($"[{engine.EngineName}] Pobrano slot semafora systemowego {WorkerSemaphoreName}.");
            return await RunSessionAsync(channel, engine);
        }
        finally
        {
            processLimit.Release();
            Console.WriteLine($"[{engine.EngineName}] Zwolniono slot semafora systemowego.");
        }
    }

    private static async Task<int> RunSessionAsync(AsyncPipeChannel channel, IWorkerEngine engine)
    {
        using var sessionCts = new CancellationTokenSource();
        await using var incoming = channel.ReadMessagesAsync(sessionCts.Token).GetAsyncEnumerator(sessionCts.Token);

        if (!await incoming.MoveNextAsync() || incoming.Current.MessageType != IpcMessageTypes.Start)
        {
            Console.Error.WriteLine("Nie otrzymano konfiguracji startowej.");
            return 3;
        }

        var configuration = incoming.Current.ReadPayload<WorkerStartConfiguration>()
            ?? throw new InvalidOperationException("Pusta konfiguracja startowa.");

        TspInstance instance;
        Action<int[], Random>? customMutation = null;
        try
        {
            instance = TspLibParser.ParseFile(configuration.DataFilePath);
            if (!string.IsNullOrWhiteSpace(configuration.MutationScript))
            {
                customMutation = await TourScriptCompiler.CompileAsync(configuration.MutationScript, sessionCts.Token);
            }
        }
        catch (Exception ex)
        {
            await channel.SendAsync(IpcMessageTypes.WorkerError, new WorkerErrorPayload(ex.Message));
            return 4;
        }

        var settings = new OptimizationSettings(
            configuration.WorkerCount,
            configuration.MaxEpochs,
            TimeSpan.FromSeconds(configuration.Phase1Seconds),
            TimeSpan.FromSeconds(configuration.Phase2Seconds));

        using var coordinator = new MemeticEpochCoordinator(instance, settings, engine, engine, customMutation);

        await channel.SendAsync(IpcMessageTypes.WorkerReady, new WorkerReadyPayload(
            engine.EngineName, Environment.ProcessId, instance.Name, instance.Dimension, customMutation is not null));
        Console.WriteLine($"[{engine.EngineName}] Instancja {instance.Name} ({instance.Dimension} miast), N = {settings.WorkerCount}, epoki = {settings.MaxEpochs}.");

        var dispatchTask = engine.RunDispatchLoopAsync(envelope => SendSafelyAsync(channel, coordinator, envelope), CancellationToken.None);
        var telemetryTask = RunTelemetryLoopAsync(channel, coordinator, sessionCts.Token);
        var commandTask = RunCommandLoopAsync(incoming, coordinator);

        string? failure = null;
        try
        {
            await engine.RunCoordinatorAsync(coordinator.Run);
        }
        catch (Exception ex)
        {
            failure = ex.GetBaseException().Message;
        }

        engine.Complete();
        await dispatchTask;
        sessionCts.Cancel();
        await telemetryTask;

        if (channel.IsConnected)
        {
            await channel.SendAsync(IpcMessageTypes.TelemetryTick, coordinator.CaptureTelemetry());
            if (failure is not null)
            {
                await channel.SendAsync(IpcMessageTypes.WorkerError, new WorkerErrorPayload(failure));
            }

            await channel.SendAsync(IpcMessageTypes.WorkerFinished, coordinator.CaptureFinishedSummary());
        }

        await commandTask;
        var summary = coordinator.CaptureFinishedSummary();
        Console.WriteLine($"[{engine.EngineName}] Koniec: {summary.Reason}. Najlepszy koszt {summary.BestCost:F2}, ocenionych rozwiązań {summary.TotalEvaluated}.");
        return failure is null ? 0 : 5;
    }

    private static async Task RejectSessionAsync(AsyncPipeChannel channel, string message)
    {
        using var timeout = new CancellationTokenSource(ConnectTimeout);
        try
        {
            await using var incoming = channel.ReadMessagesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
            await incoming.MoveNextAsync();
            await channel.SendAsync(IpcMessageTypes.WorkerError, new WorkerErrorPayload(message), timeout.Token);
            await channel.SendAsync(IpcMessageTypes.WorkerFinished,
                new WorkerFinishedPayload("Odrzucono – brak wolnego slotu semafora", 0, -1, 0, 0, 0), timeout.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
        }
    }

    private static async Task SendSafelyAsync(AsyncPipeChannel channel, MemeticEpochCoordinator coordinator, IpcEnvelope envelope)
    {
        try
        {
            await channel.SendAsync(envelope, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            coordinator.Stop("Utracono połączenie z GUI");
        }
    }

    private static async Task RunTelemetryLoopAsync(AsyncPipeChannel channel, MemeticEpochCoordinator coordinator, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TelemetryInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await channel.SendAsync(IpcMessageTypes.TelemetryTick, coordinator.CaptureTelemetry(), ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
            coordinator.Stop("Utracono połączenie z GUI");
        }
    }

    private static async Task RunCommandLoopAsync(IAsyncEnumerator<IpcMessage> incoming, MemeticEpochCoordinator coordinator)
    {
        try
        {
            while (await incoming.MoveNextAsync())
            {
                var message = incoming.Current;
                switch (message.MessageType)
                {
                    case IpcMessageTypes.Pause:
                        coordinator.Pause();
                        break;
                    case IpcMessageTypes.Resume:
                        coordinator.Resume();
                        break;
                    case IpcMessageTypes.Stop:
                        coordinator.Stop();
                        break;
                    case IpcMessageTypes.UpdatePhaseTimes:
                        var times = message.ReadPayload<PhaseTimesUpdate>();
                        if (times is not null)
                        {
                            coordinator.UpdatePhaseTimes(TimeSpan.FromSeconds(times.Phase1Seconds), TimeSpan.FromSeconds(times.Phase2Seconds));
                        }
                        break;
                }
            }

            coordinator.Stop("Utracono połączenie z GUI");
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
            coordinator.Stop("Utracono połączenie z GUI");
        }
    }
}
