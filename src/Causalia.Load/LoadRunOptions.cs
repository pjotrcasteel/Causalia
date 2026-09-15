using Causalia.Load.Thresholds;

namespace Causalia.Load;

/// <summary>
/// Configures deterministic load execution behavior and optional result thresholds.
/// </summary>
public sealed class LoadRunOptions
{
    /// <summary>
    /// Gets or initializes how iteration exceptions are handled.
    /// </summary>
    public LoadIterationFailureMode FailureMode { get; init; } = LoadIterationFailureMode.StopOnFirstFailure;

    /// <summary>
    /// Gets or initializes thresholds that are evaluated after the workload completes.
    /// </summary>
    public LoadThresholds? Thresholds { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of representative iteration failures retained in the result.
    /// </summary>
    public int MaximumFailureSamples { get; init; } = 100;

    /// <summary>
    /// Gets or initializes the maximum number of iterations retained for exact latency statistics.
    /// </summary>
    public long MaximumIterations { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or initializes whether per-iteration start, completion and drop events are written to the simulation trace.
    /// </summary>
    public bool TraceIterations { get; init; }
}
