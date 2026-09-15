namespace Causalia.FailureIntelligence;

/// <summary>
/// Classifies the semantic reason a deterministic simulation failed.
/// </summary>
public enum FailureKind
{
    /// <summary>
    /// Application or test code threw an exception that does not have a more specific Causalia classification.
    /// </summary>
    UnhandledException,

    /// <summary>
    /// A registered safety or liveness invariant was violated.
    /// </summary>
    InvariantViolation,

    /// <summary>
    /// An invariant predicate threw while Causalia evaluated it.
    /// </summary>
    InvariantEvaluation,

    /// <summary>
    /// A completed concurrent history had no legal sequential explanation.
    /// </summary>
    LinearizabilityViolation,

    /// <summary>
    /// Linearizability search bounds were exhausted before a proof could be reached.
    /// </summary>
    LinearizabilityInconclusive,

    /// <summary>
    /// A distributed-consistency guarantee was violated.
    /// </summary>
    ConsistencyViolation,

    /// <summary>
    /// The system-under-test observation diverged from an executable model.
    /// </summary>
    ModelViolation,

    /// <summary>
    /// Incomplete simulation work remained with no runnable work or virtual timer capable of making progress.
    /// </summary>
    Deadlock,

    /// <summary>
    /// The configured deterministic scheduler step limit was exceeded.
    /// </summary>
    StepLimitExceeded
}
