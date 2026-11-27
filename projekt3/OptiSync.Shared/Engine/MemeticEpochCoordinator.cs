using System.Diagnostics;
using OptiSync.Shared.Algorithms;
using OptiSync.Shared.Domain;
using OptiSync.Shared.Ipc;
using OptiSync.Shared.Synchronization;

namespace OptiSync.Shared.Engine;

public sealed class MemeticEpochCoordinator : IDisposable
{
    private readonly TspInstance _instance;
    private readonly IWorkerScheduler _scheduler;
    private readonly ITelemetrySink _sink;
    private readonly TspMemeticOperators _operators;
    private readonly EvaluationTelemetry _telemetry = new();
    private readonly BestSolutionVault _vault;
    private readonly WorkerStatsRegistry _workerStats = new();
    private readonly ManualResetEventSlim _pauseGate = new(initialState: true);
    private readonly CancellationTokenSource _stopSource = new();
    private readonly PhaseClock _phaseClock = new();
    private readonly PopulationExchange _explorationCandidates;
    private readonly PopulationExchange _intensifiedCandidates;
    private readonly Stopwatch _elapsed = new();
    private readonly int _workerCount;
    private readonly int _maxEpochs;
    private readonly int[][] _parents;
    private readonly double[] _parentCosts;
    private readonly int[][] _survivors;
    private readonly double[] _survivorCosts;

    private Barrier? _barrier;
    private CancellationToken _phaseToken;
    private long _phase1DurationTicks;
    private long _phase2DurationTicks;
    private volatile bool _terminate;
    private volatile int _epoch;
    private volatile int _completedEpochs;
    private volatile int _phase = (int)MemeticPhase.Initialization;
    private string _finishReason = "Zakończono wszystkie epoki";

    public MemeticEpochCoordinator(TspInstance instance, OptimizationSettings settings, IWorkerScheduler scheduler,
        ITelemetrySink sink, Action<int[], Random>? customMutation = null)
    {
        if (settings.WorkerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Liczba wątków N musi być dodatnia.");
        }

        _instance = instance;
        _scheduler = scheduler;
        _sink = sink;
        _workerCount = settings.WorkerCount;
        _maxEpochs = Math.Max(1, settings.MaxEpochs);
        _operators = new TspMemeticOperators(instance.Dimension, instance.DistanceMatrix, settings.MutationRate, customMutation);
        _vault = new BestSolutionVault(_telemetry);
        _explorationCandidates = new PopulationExchange(2 * _workerCount, instance.Dimension, OptimizationSettings.ExchangeLockTimeout);
        _intensifiedCandidates = new PopulationExchange(_workerCount, instance.Dimension, OptimizationSettings.ExchangeLockTimeout);
        _parents = CreateTours(2 * _workerCount, instance.Dimension);
        _parentCosts = new double[2 * _workerCount];
        _survivors = CreateTours(_workerCount, instance.Dimension);
        _survivorCosts = new double[_workerCount];
        UpdatePhaseTimes(settings.Phase1Duration, settings.Phase2Duration);
    }

    public bool IsPaused => !_pauseGate.IsSet;

    public int CurrentEpoch => _epoch;

    public MemeticPhase CurrentPhase => (MemeticPhase)_phase;

    public BestSolutionSnapshot? BestSolution => _vault.ReadSnapshot();

    public void Pause()
    {
        _phaseClock.Pause();
        _pauseGate.Reset();
        Log("Pauza: bramka ManualResetEventSlim zamknięta.");
    }

    public void Resume()
    {
        _pauseGate.Set();
        _phaseClock.Resume();
        Log("Wznowienie: bramka ManualResetEventSlim otwarta.");
    }

    public void Stop(string reason = "Zatrzymano na żądanie użytkownika")
    {
        if (_stopSource.IsCancellationRequested || CurrentPhase == MemeticPhase.Finished)
        {
            return;
        }

        _finishReason = reason;
        _stopSource.Cancel();
        _pauseGate.Set();
    }

    public void UpdatePhaseTimes(TimeSpan phase1, TimeSpan phase2)
    {
        Interlocked.Exchange(ref _phase1DurationTicks, Math.Max(TimeSpan.FromMilliseconds(100).Ticks, phase1.Ticks));
        Interlocked.Exchange(ref _phase2DurationTicks, Math.Max(TimeSpan.FromMilliseconds(100).Ticks, phase2.Ticks));
    }

    public TelemetryTickPayload CaptureTelemetry()
    {
        double elapsedSeconds = _elapsed.Elapsed.TotalSeconds;
        long total = _telemetry.TotalEvaluated;
        double lowest = _telemetry.LowestCost;
        return new TelemetryTickPayload(
            total,
            lowest == double.MaxValue ? 0 : lowest,
            _vault.ReadLeaderWorkerId(),
            _epoch,
            _maxEpochs,
            _completedEpochs,
            CurrentPhase.ToDisplayName(),
            _phaseClock.Remaining.TotalSeconds,
            elapsedSeconds,
            elapsedSeconds > 0 ? total / elapsedSeconds : 0,
            IsPaused,
            _operators.RejectedScriptResults,
            _workerStats.Snapshot());
    }

    public WorkerFinishedPayload CaptureFinishedSummary()
    {
        var best = _vault.ReadSnapshot();
        return new WorkerFinishedPayload(
            _finishReason,
            best?.Cost ?? 0,
            best?.WorkerId ?? -1,
            _telemetry.TotalEvaluated,
            _elapsed.Elapsed.TotalSeconds,
            _completedEpochs);
    }

    public void Run()
    {
        _elapsed.Start();
        try
        {
            RunInitializationPhase();
            if (_stopSource.IsCancellationRequested)
            {
                return;
            }

            RunEpochs();
        }
        finally
        {
            _phase = (int)MemeticPhase.Finished;
            _elapsed.Stop();
        }
    }

    public void Dispose()
    {
        _barrier?.Dispose();
        _phaseClock.Dispose();
        _stopSource.Dispose();
        _pauseGate.Dispose();
        _vault.Dispose();
        _operators.Dispose();
    }

    private void RunInitializationPhase()
    {
        _phase = (int)MemeticPhase.Initialization;
        PublishPhaseChanged(MemeticPhase.Initialization, $"Faza 0: generowanie populacji 2 * N = {2 * _workerCount} (Randomized k-NN)", 0, null);

        Exception? initializationError = null;
        using var countdown = new CountdownEvent(_workerCount);
        for (int workerId = 1; workerId <= _workerCount; workerId++)
        {
            int slot = workerId - 1;
            _scheduler.Schedule(() =>
            {
                try
                {
                    for (int offset = 0; offset < 2; offset++)
                    {
                        int index = 2 * slot + offset;
                        _parentCosts[index] = _operators.BuildRandomizedNearestNeighbor(_parents[index]);
                        _telemetry.RecordEvaluation();
                        _explorationCandidates.Submit(index, _parents[index], _parentCosts[index]);
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref initializationError, ex, null);
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        countdown.Wait();

        if (initializationError is not null)
        {
            throw new InvalidOperationException("Faza 0 zakończyła się błędem.", initializationError);
        }

        for (int index = 0; index < _parents.Length; index++)
        {
            TryPublishRecord(0, _parents[index], _parentCosts[index]);
        }

        Log($"Faza 0 zakończona: CountdownEvent zasygnalizowany przez {_workerCount} uczestników.");
    }

    private void RunEpochs()
    {
        _barrier = new Barrier(_workerCount, OnBarrierPhaseCompleted);
        _epoch = 1;
        StartPhase(MemeticPhase.Exploration);

        using var completion = new CountdownEvent(_workerCount);
        for (int workerId = 1; workerId <= _workerCount; workerId++)
        {
            int id = workerId;
            _scheduler.Schedule(() => RunWorker(id, completion));
        }

        completion.Wait();
    }

    private void RunWorker(int workerId, CountdownEvent completion)
    {
        var stats = _workerStats.Register(workerId);
        int slot = workerId - 1;
        int n = _instance.Dimension;
        var parent1 = new int[n];
        var parent2 = new int[n];
        var candidate = new int[n];
        bool leftCleanly = false;

        try
        {
            while (true)
            {
                _parents[2 * slot].CopyTo(parent1, 0);
                _parents[2 * slot + 1].CopyTo(parent2, 0);
                double cost1 = _parentCosts[2 * slot];
                double cost2 = _parentCosts[2 * slot + 1];

                RunExploration(workerId, stats, parent1, ref cost1, parent2, ref cost2, _phaseToken);

                _explorationCandidates.Submit(2 * slot, parent1, cost1);
                _explorationCandidates.Submit(2 * slot + 1, parent2, cost2);
                _barrier!.SignalAndWait();
                if (_terminate)
                {
                    break;
                }

                _survivors[slot].CopyTo(candidate, 0);
                double improvedCost = RunIntensification(workerId, stats, candidate, _survivorCosts[slot], _phaseToken);

                _intensifiedCandidates.Submit(slot, candidate, improvedCost);
                _barrier.SignalAndWait();
                if (_terminate)
                {
                    break;
                }
            }

            leftCleanly = true;
        }
        catch (Exception ex)
        {
            Log($"Wątek #{workerId} przerwany błędem: {ex.Message}. Wyrejestrowanie z bariery (RemoveParticipant).");
        }
        finally
        {
            if (!leftCleanly)
            {
                _barrier?.RemoveParticipant();
            }

            completion.Signal();
        }
    }

    private void RunExploration(int workerId, WorkerStats stats, int[] parent1, ref double cost1, int[] parent2, ref double cost2, CancellationToken phaseToken)
    {
        var pad = _operators.Scratchpad;
        var child1 = pad.ChildA;
        var child2 = pad.ChildB;

        while (!phaseToken.IsCancellationRequested)
        {
            try
            {
                _pauseGate.Wait(phaseToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _operators.CrossoverPmx(parent1, parent2, child1, child2);

            double childCost1 = _operators.MutateIfDrawn(child1) ? _operators.TourCost(child1) : double.MaxValue;
            _telemetry.RecordEvaluation();
            double childCost2 = _operators.MutateIfDrawn(child2) ? _operators.TourCost(child2) : double.MaxValue;
            _telemetry.RecordEvaluation();

            KeepBestTwo(parent1, ref cost1, parent2, ref cost2, child1, childCost1, child2, childCost2);
            stats.AddIteration();

            double pairBest = Math.Min(cost1, cost2);
            stats.ObserveCost(pairBest);
            if (pairBest < _telemetry.LowestCost)
            {
                TryPublishRecord(workerId, cost1 <= cost2 ? parent1 : parent2, pairBest, stats);
            }
        }
    }

    private double RunIntensification(int workerId, WorkerStats stats, int[] tour, double cost, CancellationToken phaseToken)
    {
        cost = _operators.ImproveTwoOpt(tour, cost, phaseToken, _pauseGate, _telemetry);
        cost = _operators.TourCost(tour);
        stats.ObserveCost(cost);
        TryPublishRecord(workerId, tour, cost, stats);

        cost = _operators.ImproveThreeOpt(tour, cost, phaseToken, _pauseGate, _telemetry,
            (improvedTour, improvedCost) =>
            {
                stats.ObserveCost(improvedCost);
                TryPublishRecord(workerId, improvedTour, improvedCost, stats);
            });

        cost = _operators.TourCost(tour);
        stats.ObserveCost(cost);
        stats.AddIteration();
        TryPublishRecord(workerId, tour, cost, stats);
        return cost;
    }

    private static void KeepBestTwo(int[] parent1, ref double cost1, int[] parent2, ref double cost2,
        int[] child1, double childCost1, int[] child2, double childCost2)
    {
        Span<double> costs = [cost1, cost2, childCost1, childCost2];
        int best = 0;
        for (int i = 1; i < 4; i++)
        {
            if (costs[i] < costs[best])
            {
                best = i;
            }
        }

        int second = best == 0 ? 1 : 0;
        for (int i = 0; i < 4; i++)
        {
            if (i != best && costs[i] < costs[second])
            {
                second = i;
            }
        }

        bool keepParent1 = best == 0 || second == 0;
        bool keepParent2 = best == 1 || second == 1;
        if (keepParent1 && keepParent2)
        {
            return;
        }

        Span<int> keptChildren = stackalloc int[2];
        int keptCount = 0;
        if (best >= 2)
        {
            keptChildren[keptCount++] = best;
        }

        if (second >= 2)
        {
            keptChildren[keptCount++] = second;
        }

        int next = 0;
        if (!keepParent1)
        {
            int childIndex = keptChildren[next++];
            (childIndex == 2 ? child1 : child2).CopyTo(parent1, 0);
            cost1 = costs[childIndex];
        }

        if (!keepParent2)
        {
            int childIndex = keptChildren[next];
            (childIndex == 2 ? child1 : child2).CopyTo(parent2, 0);
            cost2 = costs[childIndex];
        }
    }

    private void TryPublishRecord(int workerId, ReadOnlySpan<int> tour, double cost, WorkerStats? stats = null)
    {
        if (cost >= _vault.ReadBestCost() - 1e-6)
        {
            return;
        }

        if (!_vault.TryUpdateBest(workerId, tour, cost, out var snapshot) || snapshot is null)
        {
            return;
        }

        _telemetry.TryLowerCost(cost);
        stats?.AddRecord();
        _sink.Publish(new IpcEnvelope(IpcMessageTypes.NewBestTour, new NewBestTourPayload(
            snapshot.WorkerId,
            snapshot.Cost,
            snapshot.Tour,
            snapshot.TotalEvaluated,
            _epoch,
            CurrentPhase.ToDisplayName())));
    }

    private void OnBarrierPhaseCompleted(Barrier barrier)
    {
        try
        {
            if (barrier.CurrentPhaseNumber % 2 == 0)
            {
                CompleteExplorationPhase();
            }
            else
            {
                CompleteEpoch();
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd w postPhaseAction bariery: {ex.Message}");
            _finishReason = "Przerwano z powodu błędu synchronizacji";
            _terminate = true;
        }
    }

    private void CompleteExplorationPhase()
    {
        _phase = (int)MemeticPhase.BarrierSelection;
        var ranked = _explorationCandidates.TakeRankedSnapshot();
        double median = (ranked[_workerCount - 1].Cost + ranked[_workerCount].Cost) / 2.0;

        for (int slot = 0; slot < _workerCount; slot++)
        {
            ranked[slot].Tour.CopyTo(_survivors[slot], 0);
            _survivorCosts[slot] = ranked[slot].Cost;
        }

        PublishPhaseChanged(MemeticPhase.BarrierSelection,
            $"Epoka {_epoch}: bariera #1 – mediana {median:F2}, odrzucono {ranked.Length - _workerCount} z {ranked.Length} kandydatów",
            0, median);

        if (_stopSource.IsCancellationRequested)
        {
            _terminate = true;
            return;
        }

        StartPhase(MemeticPhase.Intensification);
    }

    private void CompleteEpoch()
    {
        var intensified = _intensifiedCandidates.TakeRankedSnapshot();
        var pool = new List<(int[] Tour, double Cost)>(2 * _workerCount);
        for (int slot = 0; slot < _workerCount; slot++)
        {
            pool.Add((_survivors[slot].ToArray(), _survivorCosts[slot]));
        }

        foreach (var entry in intensified)
        {
            pool.Add((entry.Tour, entry.Cost));
        }

        var shuffled = pool.ToArray();
        Random.Shared.Shuffle(shuffled);
        for (int index = 0; index < _parents.Length; index++)
        {
            shuffled[index].Tour.CopyTo(_parents[index], 0);
            _parentCosts[index] = shuffled[index].Cost;
        }

        _completedEpochs = _epoch;
        _phase = (int)MemeticPhase.EpochCompleted;
        PublishPhaseChanged(MemeticPhase.EpochCompleted,
            $"Epoka {_epoch} zakończona: bariera #2 – {_workerCount} osobników sprzed Fazy 2 + {_workerCount} ulepszonych losowo sparowano w {_workerCount} par",
            0, null);

        if (_stopSource.IsCancellationRequested)
        {
            _terminate = true;
            return;
        }

        if (_epoch >= _maxEpochs)
        {
            _finishReason = $"Zakończono wszystkie epoki ({_maxEpochs})";
            _terminate = true;
            return;
        }

        _epoch++;
        StartPhase(MemeticPhase.Exploration);
    }

    private void StartPhase(MemeticPhase phase)
    {
        TimeSpan duration = TimeSpan.FromTicks(phase == MemeticPhase.Exploration
            ? Interlocked.Read(ref _phase1DurationTicks)
            : Interlocked.Read(ref _phase2DurationTicks));
        _phaseToken = _phaseClock.Begin(duration, _stopSource.Token);
        _phase = (int)phase;
        PublishPhaseChanged(phase, $"Epoka {_epoch}: {phase.ToDisplayName()} – limit {duration.TotalSeconds:F1} s", duration.TotalSeconds, null);
    }

    private void PublishPhaseChanged(MemeticPhase phase, string description, double phaseSeconds, double? median)
    {
        double best = _vault.ReadBestCost();
        _sink.Publish(new IpcEnvelope(IpcMessageTypes.PhaseChanged, new PhaseChangedPayload(
            _epoch,
            _maxEpochs,
            _completedEpochs,
            phase.ToDisplayName(),
            description,
            phaseSeconds,
            median,
            best == double.MaxValue ? 0 : best)));
    }

    private void Log(string message) =>
        _sink.Publish(new IpcEnvelope(IpcMessageTypes.WorkerLog, new WorkerLogPayload(message)));

    private static int[][] CreateTours(int count, int cityCount)
    {
        var tours = new int[count][];
        for (int i = 0; i < count; i++)
        {
            tours[i] = new int[cityCount];
        }

        return tours;
    }
}
