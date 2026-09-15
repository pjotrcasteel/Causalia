namespace Causalia.FailureIntelligence;

/// <summary>
/// Describes which controlled simulation dimensions remain necessary to reproduce a failure.
/// </summary>
public enum FailureTrigger
{
    /// <summary>
    /// No minimization proof has been produced, so trigger attribution is not known.
    /// </summary>
    Unknown,

    /// <summary>
    /// The failure reproduces with canonical scheduling and no injected faults.
    /// </summary>
    Deterministic,

    /// <summary>
    /// At least one non-canonical scheduler choice is required and no injected fault remains essential.
    /// </summary>
    SchedulerOrdering,

    /// <summary>
    /// At least one injected fault is required and no non-canonical scheduler choice remains essential.
    /// </summary>
    FaultInjection,

    /// <summary>
    /// Both non-canonical scheduler ordering and injected faults remain necessary in the minimized reproduction.
    /// </summary>
    SchedulerOrderingAndFaultInjection
}
