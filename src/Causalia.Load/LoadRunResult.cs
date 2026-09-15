namespace Causalia.Load;

/// <summary>
/// Summarizes one completed deterministic load run.
/// </summary>
public sealed class LoadRunResult
{
    internal LoadRunResult(LoadRunResultData data)
    {
        ProfileName = data.ProfileName;
        ScheduledIterations = data.ScheduledIterations;
        StartedIterations = data.StartedIterations;
        CompletedIterations = data.CompletedIterations;
        FailedIterations = data.FailedIterations;
        DroppedIterations = data.DroppedIterations;
        PeakConcurrency = data.PeakConcurrency;
        VirtualElapsed = data.VirtualElapsed;
        Latency = data.Latency;
        Failures = data.Failures;
    }

    /// <summary>
    /// Gets the deterministic load-profile name.
    /// </summary>
    public string ProfileName { get; }

    /// <summary>
    /// Gets the number of iterations the profile attempted to schedule.
    /// </summary>
    public long ScheduledIterations { get; }

    /// <summary>
    /// Gets the number of iterations that actually started.
    /// </summary>
    public long StartedIterations { get; }

    /// <summary>
    /// Gets the number of iterations that completed successfully.
    /// </summary>
    public long CompletedIterations { get; }

    /// <summary>
    /// Gets the number of iterations that completed with a recorded exception.
    /// </summary>
    public long FailedIterations { get; }

    /// <summary>
    /// Gets the number of open-model arrivals that could not start because the concurrency limit was exhausted.
    /// </summary>
    public long DroppedIterations { get; }

    /// <summary>
    /// Gets the highest number of simultaneously active iterations.
    /// </summary>
    public int PeakConcurrency { get; }

    /// <summary>
    /// Gets the amount of virtual time elapsed while executing this workload.
    /// </summary>
    public TimeSpan VirtualElapsed { get; }

    /// <summary>
    /// Gets deterministic virtual iteration-duration statistics.
    /// </summary>
    public LoadLatencyStatistics Latency { get; }

    /// <summary>
    /// Gets recorded iteration failures when the run used record-and-continue mode.
    /// </summary>
    public IReadOnlyList<LoadFailureSample> Failures { get; }

    /// <summary>
    /// Gets the fraction of started iterations that failed.
    /// </summary>
    public double FailureRate => StartedIterations == 0 ? 0d : (double)FailedIterations / StartedIterations;

    /// <summary>
    /// Gets the fraction of scheduled iterations that were dropped before starting.
    /// </summary>
    public double DroppedRate => ScheduledIterations == 0 ? 0d : (double)DroppedIterations / ScheduledIterations;

    /// <summary>
    /// Gets successfully completed iterations per virtual second.
    /// </summary>
    public double CompletedIterationsPerVirtualSecond => VirtualElapsed <= TimeSpan.Zero
        ? CompletedIterations == 0 ? 0d : double.PositiveInfinity
        : CompletedIterations / VirtualElapsed.TotalSeconds;
}
