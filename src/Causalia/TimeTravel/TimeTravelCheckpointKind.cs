namespace Causalia.TimeTravel;

/// <summary>
/// Describes why a deterministic time-travel checkpoint was captured.
/// </summary>
public enum TimeTravelCheckpointKind
{
    /// <summary>
    /// The scenario requested this checkpoint explicitly.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// The scenario has started and registered its initial probes.
    /// </summary>
    Start = 1,

    /// <summary>
    /// A runnable work item is about to execute.
    /// </summary>
    BeforeSchedulerStep = 2,

    /// <summary>
    /// A runnable work item has completed and invariants have been observed.
    /// </summary>
    AfterSchedulerStep = 3,

    /// <summary>
    /// The simulation completed successfully.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// The simulation failed deterministically.
    /// </summary>
    Failure = 5
}
