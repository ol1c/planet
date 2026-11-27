using OptiSync.Shared.Ipc;

namespace OptiSync.Shared.Engine;

public enum MemeticPhase
{
    Initialization,
    Exploration,
    BarrierSelection,
    Intensification,
    EpochCompleted,
    Finished
}

public static class MemeticPhaseNames
{
    public static string ToDisplayName(this MemeticPhase phase) => phase switch
    {
        MemeticPhase.Initialization => "Faza 0 (Inicjalizacja)",
        MemeticPhase.Exploration => "Faza 1 (PMX)",
        MemeticPhase.BarrierSelection => "Bariera (Selekcja mediany)",
        MemeticPhase.Intensification => "Faza 2 (2-opt/3-opt)",
        MemeticPhase.EpochCompleted => "Koniec epoki",
        MemeticPhase.Finished => "Zakończono",
        _ => phase.ToString()
    };
}

public sealed record OptimizationSettings(
    int WorkerCount,
    int MaxEpochs,
    TimeSpan Phase1Duration,
    TimeSpan Phase2Duration,
    double MutationRate = 0.3)
{
    public static readonly TimeSpan ExchangeLockTimeout = TimeSpan.FromSeconds(2);
}

public interface IWorkerScheduler
{
    void Schedule(Action work);
}

public interface ITelemetrySink
{
    void Publish(IpcEnvelope envelope);
    void Complete();
}

public interface IWorkerEngine : IWorkerScheduler, ITelemetrySink
{
    string EngineName { get; }
    Task RunCoordinatorAsync(Action coordinatorBody);
    Task RunDispatchLoopAsync(Func<IpcEnvelope, Task> send, CancellationToken ct);
}
