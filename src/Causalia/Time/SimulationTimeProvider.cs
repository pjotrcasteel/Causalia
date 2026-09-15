using Causalia.Runtime;

namespace Causalia.Time;

internal sealed class SimulationTimeProvider : TimeProvider
{
    private readonly PriorityQueue<ScheduledTimerInvocation, TimerPriority> _timers = new();
    private readonly DeterministicScheduler _scheduler;
    private readonly DateTimeOffset _startTime;
    private long _currentTimestamp;
    private long _timerSequence;

    public SimulationTimeProvider(DeterministicScheduler scheduler, DateTimeOffset startTime)
    {
        _scheduler = scheduler;
        _startTime = startTime.ToUniversalTime();
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        return _startTime.AddTicks(_currentTimestamp);
    }

    public override long GetTimestamp()
    {
        return _currentTimestamp;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new SimulationTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public bool TryAdvanceToNextTimer()
    {
        DiscardStaleTimers();

        if (!_timers.TryPeek(out _, out var nextPriority))
        {
            return false;
        }

        _currentTimestamp = nextPriority.DueTimestamp;

        while (_timers.TryPeek(out var invocation, out var priority) && priority.DueTimestamp == _currentTimestamp)
        {
            _timers.Dequeue();

            if (!invocation.Timer.IsCurrent(invocation.Generation))
            {
                continue;
            }

            _scheduler.EnqueueTimer(invocation);
        }

        return true;
    }

    public TimeSpan GetElapsedVirtualTime()
    {
        return TimeSpan.FromTicks(_currentTimestamp);
    }

    public void Schedule(SimulationTimer timer, long generation, TimeSpan dueTime)
    {
        var dueTimestamp = checked(_currentTimestamp + dueTime.Ticks);
        var priority = new TimerPriority(dueTimestamp, _timerSequence++);
        _timers.Enqueue(new ScheduledTimerInvocation(timer, generation, _scheduler.CurrentExplorationOperationId), priority);
    }

    private void DiscardStaleTimers()
    {
        while (_timers.TryPeek(out var invocation, out _) && !invocation.Timer.IsCurrent(invocation.Generation))
        {
            _timers.Dequeue();
        }
    }
}
