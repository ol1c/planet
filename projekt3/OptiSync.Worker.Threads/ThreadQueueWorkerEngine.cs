using System.Collections.Concurrent;
using OptiSync.Shared.Engine;
using OptiSync.Shared.Ipc;

namespace OptiSync.Worker.Threads;

public sealed class ThreadQueueWorkerEngine : IWorkerEngine
{
    private readonly BlockingCollection<IpcEnvelope> _telemetryQueue = new(new ConcurrentQueue<IpcEnvelope>());

    public ThreadQueueWorkerEngine(int expectedConcurrency)
    {
        ThreadPool.GetMinThreads(out int workerThreads, out int ioThreads);
        ThreadPool.SetMinThreads(Math.Max(workerThreads, expectedConcurrency), ioThreads);
    }

    public string EngineName => "Thread Queue Worker Process";

    public void Schedule(Action work) => ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), work);

    public void Publish(IpcEnvelope envelope)
    {
        if (_telemetryQueue.IsAddingCompleted)
        {
            return;
        }

        try
        {
            _telemetryQueue.Add(envelope);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Complete() => _telemetryQueue.CompleteAdding();

    public Task RunCoordinatorAsync(Action coordinatorBody) =>
        RunOnDedicatedThread("OptiSync-Coordinator", coordinatorBody);

    public Task RunDispatchLoopAsync(Func<IpcEnvelope, Task> send, CancellationToken ct) =>
        RunOnDedicatedThread("OptiSync-IpcSender", () =>
        {
            foreach (var envelope in _telemetryQueue.GetConsumingEnumerable(ct))
            {
                send(envelope).GetAwaiter().GetResult();
            }
        });

    private static Task RunOnDedicatedThread(string name, Action body)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                body();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        })
        {
            Name = name,
            IsBackground = true
        };

        thread.Start();
        return completion.Task;
    }
}
