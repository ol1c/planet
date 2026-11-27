using System.Diagnostics;

namespace OptiSync.Shared.Synchronization;

public sealed class PhaseClock : IDisposable
{
    private readonly Lock _sync = new();
    private CancellationTokenSource? _timeoutSource;
    private CancellationTokenSource? _linkedSource;
    private long _deadlineTimestamp;
    private TimeSpan _remainingWhilePaused;
    private bool _paused;

    public CancellationToken Begin(TimeSpan duration, CancellationToken globalStopToken)
    {
        lock (_sync)
        {
            _linkedSource?.Dispose();
            _timeoutSource?.Dispose();

            _timeoutSource = new CancellationTokenSource();
            _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(globalStopToken, _timeoutSource.Token);

            if (_paused)
            {
                _remainingWhilePaused = duration;
            }
            else
            {
                ArmTimeout(duration);
            }

            return _linkedSource.Token;
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_paused)
            {
                return;
            }

            _paused = true;
            _remainingWhilePaused = ComputeRemaining();
            if (_timeoutSource is { IsCancellationRequested: false })
            {
                _timeoutSource.CancelAfter(Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (!_paused)
            {
                return;
            }

            _paused = false;
            if (_timeoutSource is { IsCancellationRequested: false })
            {
                ArmTimeout(_remainingWhilePaused);
            }
        }
    }

    public TimeSpan Remaining
    {
        get
        {
            lock (_sync)
            {
                return _paused ? _remainingWhilePaused : ComputeRemaining();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _linkedSource?.Dispose();
            _timeoutSource?.Dispose();
            _linkedSource = null;
            _timeoutSource = null;
        }
    }

    private void ArmTimeout(TimeSpan duration)
    {
        _deadlineTimestamp = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        _timeoutSource!.CancelAfter(duration);
    }

    private TimeSpan ComputeRemaining()
    {
        if (_timeoutSource is null || _timeoutSource.IsCancellationRequested)
        {
            return TimeSpan.Zero;
        }

        long remainingTicks = _deadlineTimestamp - Stopwatch.GetTimestamp();
        return remainingTicks <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)remainingTicks / Stopwatch.Frequency);
    }
}
