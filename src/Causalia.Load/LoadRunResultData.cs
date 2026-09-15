namespace Causalia.Load;

internal sealed class LoadRunResultData
{
    public required string ProfileName { get; init; }

    public required long ScheduledIterations { get; init; }

    public required long StartedIterations { get; init; }

    public required long CompletedIterations { get; init; }

    public required long FailedIterations { get; init; }

    public required long DroppedIterations { get; init; }

    public required int PeakConcurrency { get; init; }

    public required TimeSpan VirtualElapsed { get; init; }

    public required LoadLatencyStatistics Latency { get; init; }

    public required IReadOnlyList<LoadFailureSample> Failures { get; init; }
}
