using System.Threading.Channels;
using OptiSync.Shared.Engine;
using OptiSync.Shared.Ipc;

namespace OptiSync.Worker.TPL;

public sealed class TplWorkerEngine : IWorkerEngine
{
    private readonly Channel<IpcEnvelope> _telemetryChannel = Channel.CreateUnbounded<IpcEnvelope>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public TplWorkerEngine(int expectedConcurrency)
    {
        ThreadPool.GetMinThreads(out int workerThreads, out int ioThreads);
        ThreadPool.SetMinThreads(Math.Max(workerThreads, expectedConcurrency), ioThreads);
    }

    public string EngineName => "TPL Worker Process";

    public void Schedule(Action work) => Task.Run(work);

    public void Publish(IpcEnvelope envelope) => _telemetryChannel.Writer.TryWrite(envelope);

    public void Complete() => _telemetryChannel.Writer.TryComplete();

    public Task RunCoordinatorAsync(Action coordinatorBody) =>
        Task.Factory.StartNew(coordinatorBody, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public async Task RunDispatchLoopAsync(Func<IpcEnvelope, Task> send, CancellationToken ct)
    {
        await foreach (var envelope in _telemetryChannel.Reader.ReadAllAsync(ct))
        {
            await send(envelope);
        }
    }
}
