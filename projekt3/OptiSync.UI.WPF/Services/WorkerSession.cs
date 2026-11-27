using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using OptiSync.Shared.Ipc;

namespace OptiSync.UI.WPF.Services;

public enum WorkerEngineKind
{
    Tpl,
    ThreadQueue
}

public sealed class WorkerSession : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);

    private readonly Process _process;
    private readonly AsyncPipeChannel _channel;

    private WorkerSession(Process process, AsyncPipeChannel channel)
    {
        _process = process;
        _channel = channel;
    }

    public int ProcessId => _process.Id;

    public string ProcessName { get; private init; } = string.Empty;

    public static string ResolveExecutable(WorkerEngineKind kind)
    {
        (string folder, string name) = kind switch
        {
            WorkerEngineKind.Tpl => ("TPL", "OptiSync.Worker.TPL"),
            _ => ("Threads", "OptiSync.Worker.Threads")
        };

        string path = Path.Combine(AppContext.BaseDirectory, "Workers", folder, name + ".exe");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Nie znaleziono procesu roboczego: {path}. Zbuduj całą solucję (dotnet build).", path);
        }

        return path;
    }

    public static async Task<WorkerSession> StartAsync(WorkerEngineKind kind, WorkerStartConfiguration configuration, CancellationToken ct)
    {
        string executable = ResolveExecutable(kind);
        string pipeName = $"OptiSyncNP_{Environment.ProcessId}_{Guid.NewGuid():N}";

        var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo(executable, pipeName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executable)!
            }) ?? throw new InvalidOperationException("Nie udało się uruchomić procesu roboczego.");

            using var timeout = new CancellationTokenSource(ConnectTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            var connectTask = server.WaitForConnectionAsync(linked.Token);
            var exitTask = process.WaitForExitAsync(linked.Token);
            if (await Task.WhenAny(connectTask, exitTask) != connectTask)
            {
                throw new InvalidOperationException($"Proces roboczy zakończył się przed połączeniem (kod {process.ExitCode}).");
            }

            await connectTask;

            var channel = new AsyncPipeChannel(server);
            await channel.SendAsync(IpcMessageTypes.Start, configuration, ct);
            return new WorkerSession(process, channel) { ProcessName = Path.GetFileName(executable) };
        }
        catch
        {
            await server.DisposeAsync();
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }

            process?.Dispose();
            throw;
        }
    }

    public async IAsyncEnumerable<IpcMessage> ReadWorkerStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in _channel.ReadMessagesAsync(ct))
        {
            yield return message;
        }
    }

    public async Task SendCommandAsync<T>(string messageType, T payload, CancellationToken ct = default)
    {
        if (!_channel.IsConnected)
        {
            return;
        }

        try
        {
            await _channel.SendAsync(messageType, payload, ct);
        }
        catch (IOException)
        {
        }
    }

    public async Task<int> WaitForExitAsync(TimeSpan gracePeriod)
    {
        using var timeout = new CancellationTokenSource(gracePeriod);
        try
        {
            await _process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        return _process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        _process.Dispose();
    }
}
