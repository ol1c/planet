namespace OptiSync.Shared.Synchronization;

public sealed class EvaluationTelemetry
{
    private long _totalEvaluated;
    private double _lowestCost = double.MaxValue;

    public long TotalEvaluated => Interlocked.Read(ref _totalEvaluated);

    public double LowestCost => Volatile.Read(ref _lowestCost);

    public long RecordEvaluation() => Interlocked.Increment(ref _totalEvaluated);

    public bool TryLowerCost(double candidateCost)
    {
        double observed;
        do
        {
            observed = Volatile.Read(ref _lowestCost);
            if (candidateCost >= observed)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref _lowestCost, candidateCost, observed) != observed);

        return true;
    }
}
