namespace OptiSync.UI.WPF.Models;

public sealed record TourRow(int Position, int CityId, double X, double Y);

public sealed record WorkerRow(int WorkerId, int ManagedThreadId, long Iterations, int RecordsSet, double BestCost, bool IsLeader)
{
    public string WorkerLabel => IsLeader ? $"#{WorkerId} ★" : $"#{WorkerId}";
}
