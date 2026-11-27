using OptiSync.Shared.Synchronization;

namespace OptiSync.Shared.Algorithms;

public sealed class TspMemeticOperators : IDisposable
{
    private const double ImprovementEpsilon = 1e-7;
    private const int NearestNeighborCandidates = 3;

    private readonly int _n;
    private readonly double[] _distMatrix;
    private readonly ThreadLocal<WorkerScratchpad> _scratchpad;
    private readonly Action<int[], Random>? _customMutation;
    private readonly double _mutationRate;
    private long _rejectedScriptResults;

    public TspMemeticOperators(int cityCount, double[] distMatrix, double mutationRate, Action<int[], Random>? customMutation = null)
    {
        _n = cityCount;
        _distMatrix = distMatrix;
        _mutationRate = mutationRate;
        _customMutation = customMutation;
        _scratchpad = new ThreadLocal<WorkerScratchpad>(() => new WorkerScratchpad(_n));
    }

    public int CityCount => _n;

    public bool UsesCustomMutation => _customMutation is not null;

    public long RejectedScriptResults => Interlocked.Read(ref _rejectedScriptResults);

    public WorkerScratchpad Scratchpad => _scratchpad.Value!;

    public double TourCost(ReadOnlySpan<int> tour)
    {
        double cost = 0;
        for (int i = 0; i < _n - 1; i++)
        {
            cost += _distMatrix[tour[i] * _n + tour[i + 1]];
        }

        return cost + _distMatrix[tour[_n - 1] * _n + tour[0]];
    }

    public double BuildRandomizedNearestNeighbor(Span<int> tour)
    {
        var pad = _scratchpad.Value!;
        Array.Fill(pad.Visited, false);
        Span<int> candidates = stackalloc int[NearestNeighborCandidates];
        Span<double> candidateDistances = stackalloc double[NearestNeighborCandidates];

        int current = Random.Shared.Next(_n);
        tour[0] = current;
        pad.Visited[current] = true;

        for (int position = 1; position < _n; position++)
        {
            int found = 0;
            for (int city = 0; city < _n; city++)
            {
                if (pad.Visited[city])
                {
                    continue;
                }

                double distance = _distMatrix[current * _n + city];
                if (found < NearestNeighborCandidates)
                {
                    candidates[found] = city;
                    candidateDistances[found] = distance;
                    found++;
                    continue;
                }

                int worst = 0;
                for (int k = 1; k < NearestNeighborCandidates; k++)
                {
                    if (candidateDistances[k] > candidateDistances[worst])
                    {
                        worst = k;
                    }
                }

                if (distance < candidateDistances[worst])
                {
                    candidates[worst] = city;
                    candidateDistances[worst] = distance;
                }
            }

            current = candidates[Random.Shared.Next(found)];
            tour[position] = current;
            pad.Visited[current] = true;
        }

        return TourCost(tour);
    }

    public void CrossoverPmx(ReadOnlySpan<int> parent1, ReadOnlySpan<int> parent2, Span<int> child1, Span<int> child2)
    {
        var pad = _scratchpad.Value!;
        int cut1 = Random.Shared.Next(0, _n - 1);
        int cut2 = Random.Shared.Next(cut1 + 1, _n);

        ExecutePmx(parent1, parent2, child1, cut1, cut2, pad.Mapping1);
        ExecutePmx(parent2, parent1, child2, cut1, cut2, pad.Mapping2);
    }

    public bool MutateIfDrawn(int[] tour)
    {
        if (Random.Shared.NextDouble() >= _mutationRate)
        {
            return true;
        }

        if (_customMutation is null)
        {
            ApplyInversionMutation(tour);
            return true;
        }

        try
        {
            _customMutation(tour, Random.Shared);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref _rejectedScriptResults);
            return false;
        }

        if (IsPermutation(tour))
        {
            return true;
        }

        Interlocked.Increment(ref _rejectedScriptResults);
        return false;
    }

    public bool IsPermutation(ReadOnlySpan<int> tour)
    {
        if (tour.Length != _n)
        {
            return false;
        }

        var visited = _scratchpad.Value!.Visited;
        Array.Fill(visited, false);
        foreach (int city in tour)
        {
            if ((uint)city >= (uint)_n || visited[city])
            {
                return false;
            }

            visited[city] = true;
        }

        return true;
    }

    public double ImproveTwoOpt(int[] tour, double currentCost, CancellationToken phaseToken, ManualResetEventSlim pauseGate, EvaluationTelemetry telemetry)
    {
        bool improved = true;
        while (improved && !phaseToken.IsCancellationRequested)
        {
            improved = false;

            for (int i = 0; i < _n - 2; i++)
            {
                if (!PassGate(pauseGate, phaseToken))
                {
                    return currentCost;
                }

                int a = tour[i];
                int b = tour[i + 1];
                double distAB = _distMatrix[a * _n + b];

                for (int j = i + 2; j < _n; j++)
                {
                    if (i == 0 && j == _n - 1)
                    {
                        continue;
                    }

                    int c = tour[j];
                    int d = tour[(j + 1) % _n];

                    double delta = (_distMatrix[a * _n + c] + _distMatrix[b * _n + d])
                                 - (distAB + _distMatrix[c * _n + d]);

                    if (delta < -ImprovementEpsilon)
                    {
                        Array.Reverse(tour, i + 1, j - i);
                        currentCost += delta;
                        telemetry.RecordEvaluation();
                        b = tour[i + 1];
                        distAB = _distMatrix[a * _n + b];
                        improved = true;
                    }
                }
            }
        }

        return currentCost;
    }

    public double ImproveThreeOpt(int[] tour, double currentCost, CancellationToken phaseToken, ManualResetEventSlim pauseGate,
        EvaluationTelemetry telemetry, Action<int[], double>? onPassImproved = null)
    {
        var buffer = _scratchpad.Value!.SegmentBuffer;
        bool improved = true;

        while (improved && !phaseToken.IsCancellationRequested)
        {
            improved = false;

            for (int i = 0; i < _n && !improved; i++)
            {
                if (!PassGate(pauseGate, phaseToken))
                {
                    return currentCost;
                }

                for (int j = i + 2; j < _n && !improved; j++)
                {
                    int kLimit = _n + (i > 0 ? 1 : 0);
                    for (int k = j + 2; k < kLimit; k++)
                    {
                        double delta = TryApplyThreeOptMove(tour, i, j, k, buffer);
                        if (delta < -ImprovementEpsilon)
                        {
                            currentCost += delta;
                            telemetry.RecordEvaluation();
                            improved = true;
                            break;
                        }
                    }
                }
            }

            if (improved)
            {
                onPassImproved?.Invoke(tour, currentCost);
            }
        }

        return currentCost;
    }

    public void Dispose() => _scratchpad.Dispose();

    private void ExecutePmx(ReadOnlySpan<int> parent1, ReadOnlySpan<int> parent2, Span<int> child, int cut1, int cut2, int[] mapping)
    {
        Array.Fill(mapping, -1);

        for (int i = cut1; i <= cut2; i++)
        {
            int city1 = parent1[i];
            child[i] = city1;
            mapping[city1] = parent2[i];
        }

        for (int i = 0; i < _n; i++)
        {
            if (i >= cut1 && i <= cut2)
            {
                continue;
            }

            int candidate = parent2[i];
            while (mapping[candidate] != -1)
            {
                candidate = mapping[candidate];
            }

            child[i] = candidate;
        }
    }

    private void ApplyInversionMutation(int[] tour)
    {
        int first = Random.Shared.Next(_n);
        int second = Random.Shared.Next(_n);
        if (first == second)
        {
            return;
        }

        int start = Math.Min(first, second);
        int length = Math.Abs(first - second) + 1;
        Array.Reverse(tour, start, length);
    }

    private double TryApplyThreeOptMove(int[] tour, int i, int j, int k, int[] buffer)
    {
        int a = tour[(i - 1 + _n) % _n];
        int b = tour[i];
        int c = tour[j - 1];
        int d = tour[j];
        int e = tour[k - 1];
        int f = tour[k % _n];

        double d0 = Dist(a, b) + Dist(c, d) + Dist(e, f);
        double d1 = Dist(a, c) + Dist(b, d) + Dist(e, f);
        double d2 = Dist(a, b) + Dist(c, e) + Dist(d, f);
        double d3 = Dist(a, d) + Dist(e, b) + Dist(c, f);
        double d4 = Dist(f, b) + Dist(c, d) + Dist(e, a);

        if (d1 < d0 - ImprovementEpsilon)
        {
            Array.Reverse(tour, i, j - i);
            return d1 - d0;
        }

        if (d2 < d0 - ImprovementEpsilon)
        {
            Array.Reverse(tour, j, k - j);
            return d2 - d0;
        }

        if (d4 < d0 - ImprovementEpsilon)
        {
            Array.Reverse(tour, i, k - i);
            return d4 - d0;
        }

        if (d3 < d0 - ImprovementEpsilon)
        {
            int firstLength = j - i;
            int secondLength = k - j;
            Array.Copy(tour, j, buffer, 0, secondLength);
            Array.Copy(tour, i, buffer, secondLength, firstLength);
            Array.Copy(buffer, 0, tour, i, firstLength + secondLength);
            return d3 - d0;
        }

        return 0;
    }

    private double Dist(int from, int to) => _distMatrix[from * _n + to];

    private static bool PassGate(ManualResetEventSlim pauseGate, CancellationToken phaseToken)
    {
        if (phaseToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            pauseGate.Wait(phaseToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
