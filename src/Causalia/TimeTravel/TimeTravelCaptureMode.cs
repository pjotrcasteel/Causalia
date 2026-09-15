namespace Causalia.TimeTravel;

/// <summary>
/// Controls when Causalia captures deterministic time-travel checkpoints.
/// </summary>
public enum TimeTravelCaptureMode
{
    /// <summary>
    /// Captures only checkpoints requested explicitly by the scenario.
    /// </summary>
    ManualOnly = 0,

    /// <summary>
    /// Captures the start, both sides of every scheduler step, and terminal completion or failure.
    /// </summary>
    SchedulerSteps = 1
}
