namespace Causalia.TimeTravel;

/// <summary>
/// Configures deterministic state capture for the time-travel debugger.
/// </summary>
public sealed class TimeTravelOptions
{
    /// <summary>
    /// Gets or initializes when automatic checkpoints are captured.
    /// </summary>
    public TimeTravelCaptureMode CaptureMode { get; init; } = TimeTravelCaptureMode.ManualOnly;

    /// <summary>
    /// Gets or initializes the maximum number of checkpoints retained in memory.
    /// Older checkpoints are discarded after the limit is reached.
    /// </summary>
    public int MaxRetainedCheckpoints { get; init; } = 10_000;
}
