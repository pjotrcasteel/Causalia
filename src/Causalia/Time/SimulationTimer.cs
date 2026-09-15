namespace Causalia.Time;

internal sealed class SimulationTimer : ITimer
{
    private readonly SimulationTimeProvider _timeProvider;
    private readonly TimerCallback _callback;
    private readonly object? _state;
    private bool _disposed;
    private long _generation;
    private TimeSpan _period;

    public SimulationTimer(SimulationTimeProvider timeProvider, TimerCallback callback, object? state)
    {
        _timeProvider = timeProvider;
        _callback = callback;
        _state = state;
        _period = Timeout.InfiniteTimeSpan;
    }

    public long Generation => _generation;

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTimeout(dueTime, nameof(dueTime));
        ValidateTimeout(period, nameof(period));

        _generation++;
        _period = NormalizePeriod(period);

        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            _timeProvider.Schedule(this, _generation, dueTime);
        }

        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        _generation++;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public bool IsCurrent(long generation)
    {
        return !_disposed && generation == _generation;
    }

    public void Fire(long generation)
    {
        if (!IsCurrent(generation))
        {
            return;
        }

        if (_period != Timeout.InfiniteTimeSpan)
        {
            _timeProvider.Schedule(this, generation, _period);
        }
        else
        {
            _generation++;
        }

        _callback(_state);
    }

    private static TimeSpan NormalizePeriod(TimeSpan period)
    {
        return period == TimeSpan.Zero ? Timeout.InfiniteTimeSpan : period;
    }

    private static void ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout < Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
