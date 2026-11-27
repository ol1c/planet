namespace OptiSync.Shared.Synchronization;

public sealed record BestSolutionSnapshot(int WorkerId, double Cost, int[] Tour, long TotalEvaluated);

public sealed class BestSolutionVault : IDisposable
{
    private const double ImprovementTolerance = 1e-6;

    private readonly ReaderWriterLockSlim _rwLock = new();
    private readonly EvaluationTelemetry _telemetry;
    private double _bestCost = double.MaxValue;
    private int[] _bestTour = [];
    private int _discovererWorkerId = -1;

    public BestSolutionVault(EvaluationTelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    public double ReadBestCost()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _bestCost;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public int ReadLeaderWorkerId()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _discovererWorkerId;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public BestSolutionSnapshot? ReadSnapshot()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _bestTour.Length == 0
                ? null
                : new BestSolutionSnapshot(_discovererWorkerId, _bestCost, _bestTour.ToArray(), _telemetry.TotalEvaluated);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public bool TryUpdateBest(int workerId, ReadOnlySpan<int> candidateTour, double candidateCost, out BestSolutionSnapshot? newRecordSnapshot)
    {
        newRecordSnapshot = null;

        if (candidateCost >= Volatile.Read(ref _bestCost) - ImprovementTolerance)
        {
            return false;
        }

        _rwLock.EnterWriteLock();
        try
        {
            if (candidateCost >= _bestCost - ImprovementTolerance)
            {
                return false;
            }

            _bestCost = candidateCost;
            _discovererWorkerId = workerId;
            if (_bestTour.Length != candidateTour.Length)
            {
                _bestTour = new int[candidateTour.Length];
            }

            candidateTour.CopyTo(_bestTour);
            newRecordSnapshot = new BestSolutionSnapshot(workerId, candidateCost, _bestTour.ToArray(), _telemetry.TotalEvaluated);
            return true;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public void Dispose() => _rwLock.Dispose();
}
