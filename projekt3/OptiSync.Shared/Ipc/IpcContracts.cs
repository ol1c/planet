using System.Text.Json;

namespace OptiSync.Shared.Ipc;

public static class IpcMessageTypes
{
    public const string Start = "Start";
    public const string Pause = "Pause";
    public const string Resume = "Resume";
    public const string Stop = "Stop";
    public const string UpdatePhaseTimes = "UpdatePhaseTimes";

    public const string WorkerReady = "WorkerReady";
    public const string NewBestTour = "NewBestTour";
    public const string PhaseChanged = "PhaseChanged";
    public const string TelemetryTick = "TelemetryTick";
    public const string WorkerLog = "WorkerLog";
    public const string WorkerError = "WorkerError";
    public const string WorkerFinished = "WorkerFinished";
}

public record IpcMessage(string MessageType, string JsonPayload)
{
    public T? ReadPayload<T>() => JsonSerializer.Deserialize<T>(JsonPayload, IpcJson.Options);
}

public sealed record IpcEnvelope(string MessageType, object Payload);

public static class IpcJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);
}

public sealed record EmptyPayload
{
    public static readonly EmptyPayload Instance = new();
}

public sealed record WorkerStartConfiguration(
    string DataFilePath,
    int WorkerCount,
    int MaxEpochs,
    double Phase1Seconds,
    double Phase2Seconds,
    string? MutationScript);

public sealed record PhaseTimesUpdate(double Phase1Seconds, double Phase2Seconds);

public sealed record WorkerReadyPayload(string Engine, int ProcessId, string InstanceName, int CityCount, bool UsesScript);

public sealed record NewBestTourPayload(int WorkerId, double Cost, int[] Tour, long TotalEvaluated, int Epoch, string Phase);

public sealed record PhaseChangedPayload(
    int Epoch,
    int MaxEpochs,
    int CompletedEpochs,
    string Phase,
    string Description,
    double PhaseSeconds,
    double? MedianCost,
    double BestCost);

public sealed record WorkerStatsDto(int WorkerId, int ManagedThreadId, long Iterations, int RecordsSet, double BestCost);

public sealed record TelemetryTickPayload(
    long TotalEvaluated,
    double LowestCost,
    int LeaderWorkerId,
    int Epoch,
    int MaxEpochs,
    int CompletedEpochs,
    string Phase,
    double PhaseRemainingSeconds,
    double ElapsedSeconds,
    double SolutionsPerSecond,
    bool IsPaused,
    long RejectedScriptResults,
    WorkerStatsDto[] Workers);

public sealed record WorkerLogPayload(string Message);

public sealed record WorkerErrorPayload(string Message);

public sealed record WorkerFinishedPayload(
    string Reason,
    double BestCost,
    int LeaderWorkerId,
    long TotalEvaluated,
    double ElapsedSeconds,
    int CompletedEpochs);
