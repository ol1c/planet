namespace OptiSync.Shared.Synchronization;

public readonly record struct RankedTour(int Slot, int[] Tour, double Cost);

public sealed class PopulationExchange
{
    private readonly object _exchangeLock = new();
    private readonly TimeSpan _lockTimeout;
    private readonly int[][] _tours;
    private readonly double[] _costs;

    public PopulationExchange(int slotCount, int cityCount, TimeSpan lockTimeout)
    {
        _lockTimeout = lockTimeout;
        _tours = new int[slotCount][];
        _costs = new double[slotCount];
        for (int slot = 0; slot < slotCount; slot++)
        {
            _tours[slot] = new int[cityCount];
            _costs[slot] = double.MaxValue;
        }
    }

    public int SlotCount => _tours.Length;

    public void Submit(int slot, ReadOnlySpan<int> tour, double cost)
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(_exchangeLock, _lockTimeout, ref lockTaken);
            if (!lockTaken)
            {
                throw new TimeoutException($"Nie udało się zgłosić kandydata do slotu {slot} w czasie {_lockTimeout.TotalMilliseconds} ms.");
            }

            tour.CopyTo(_tours[slot]);
            _costs[slot] = cost;
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(_exchangeLock);
            }
        }
    }

    public RankedTour[] TakeRankedSnapshot()
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(_exchangeLock, _lockTimeout, ref lockTaken);
            if (!lockTaken)
            {
                throw new TimeoutException("Przekroczono limit czasu oczekiwania na wymianę populacji.");
            }

            var snapshot = new RankedTour[_tours.Length];
            for (int slot = 0; slot < _tours.Length; slot++)
            {
                snapshot[slot] = new RankedTour(slot, _tours[slot].ToArray(), _costs[slot]);
            }

            Array.Sort(snapshot, static (left, right) => left.Cost.CompareTo(right.Cost));
            return snapshot;
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(_exchangeLock);
            }
        }
    }
}
