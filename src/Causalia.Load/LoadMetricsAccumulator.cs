namespace Causalia.Load;

internal sealed class LoadMetricsAccumulator
{
    private readonly int _maximumFailureSamples;
    private readonly List<LoadFailureSample> _failures = [];
    private readonly List<long> _latencyTicks = [];
    private int _active;

    public LoadMetricsAccumulator(int maximumFailureSamples)
    {
        _maximumFailureSamples = maximumFailureSamples;
    }

    public long ScheduledIterations { get; private set; }

    public long StartedIterations { get; private set; }

    public long CompletedIterations { get; private set; }

    public long FailedIterations { get; private set; }

    public long DroppedIterations { get; private set; }

    public int PeakConcurrency { get; private set; }

    public IReadOnlyList<LoadFailureSample> Failures => _failures.AsReadOnly();

    public void Scheduled()
    {
        ScheduledIterations++;
    }

    public void Dropped()
    {
        DroppedIterations++;
    }

    public void Started()
    {
        StartedIterations++;
        _active++;

        if (_active > PeakConcurrency)
        {
            PeakConcurrency = _active;
        }
    }

    public void Completed(long durationTicks)
    {
        CompletedIterations++;
        Finished(durationTicks);
    }

    public void Failed(long iterationId, int actorId, long durationTicks, Exception exception)
    {
        FailedIterations++;
        Finished(durationTicks);

        if (_failures.Count >= _maximumFailureSamples)
        {
            return;
        }

        _failures.Add(
            new LoadFailureSample(
                iterationId,
                actorId,
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message));
    }

    public LoadLatencyStatistics CreateLatencyStatistics()
    {
        if (_latencyTicks.Count == 0)
        {
            return new LoadLatencyStatistics(
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);
        }

        _latencyTicks.Sort();
        var total = 0m;

        foreach (var ticks in _latencyTicks)
        {
            total += ticks;
        }

        var meanTicks = decimal.ToInt64(decimal.Round(total / _latencyTicks.Count, 0, MidpointRounding.AwayFromZero));
        return new LoadLatencyStatistics(
            TimeSpan.FromTicks(_latencyTicks[0]),
            TimeSpan.FromTicks(meanTicks),
            TimeSpan.FromTicks(PercentileTicks(0.50m)),
            TimeSpan.FromTicks(PercentileTicks(0.95m)),
            TimeSpan.FromTicks(PercentileTicks(0.99m)),
            TimeSpan.FromTicks(_latencyTicks[^1]));
    }

    private void Finished(long durationTicks)
    {
        _active--;
        _latencyTicks.Add(durationTicks);
    }

    private long PercentileTicks(decimal percentile)
    {
        var rank = decimal.Ceiling(percentile * _latencyTicks.Count);
        var index = Math.Max(0, decimal.ToInt32(rank) - 1);
        return _latencyTicks[index];
    }
}
