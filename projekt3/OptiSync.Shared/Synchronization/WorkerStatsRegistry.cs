using System.Collections.Concurrent;
using OptiSync.Shared.Ipc;

namespace OptiSync.Shared.Synchronization;

public sealed class WorkerStats
{
    private long _iterations;
    private int _recordsSet;
    private int _managedThreadId;
    private double _bestCost = double.MaxValue;

    public WorkerStats(int workerId)
    {
        WorkerId = workerId;
    }

    public int WorkerId { get; }

    public void BindToCurrentThread() => Volatile.Write(ref _managedThreadId, Environment.CurrentManagedThreadId);

    public void AddIteration() => Interlocked.Increment(ref _iterations);

    public void AddRecord() => Interlocked.Increment(ref _recordsSet);

    public void ObserveCost(double cost)
    {
        double observed;
        do
        {
            observed = Volatile.Read(ref _bestCost);
            if (cost >= observed)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _bestCost, cost, observed) != observed);
    }

    public WorkerStatsDto ToDto()
    {
        double best = Volatile.Read(ref _bestCost);
        return new WorkerStatsDto(
            WorkerId,
            Volatile.Read(ref _managedThreadId),
            Interlocked.Read(ref _iterations),
            Volatile.Read(ref _recordsSet),
            best == double.MaxValue ? 0 : best);
    }
}

public sealed class WorkerStatsRegistry
{
    private readonly ConcurrentDictionary<int, WorkerStats> _stats = new();

    public WorkerStats Register(int workerId)
    {
        var stats = _stats.GetOrAdd(workerId, static id => new WorkerStats(id));
        stats.BindToCurrentThread();
        return stats;
    }

    public WorkerStatsDto[] Snapshot() =>
        _stats.Values
            .Select(stats => stats.ToDto())
            .OrderBy(dto => dto.WorkerId)
            .ToArray();
}
