namespace Causalia.Consistency;

/// <summary>
/// Identifies the consistency rule that was violated by a recorded history.
/// </summary>
public enum ConsistencyViolationKind
{
    /// <summary>
    /// A session failed to observe its own previous write.
    /// </summary>
    ReadYourWrites,

    /// <summary>
    /// A session observed a version older than a version it had already read.
    /// </summary>
    MonotonicReads,

    /// <summary>
    /// A replica exposed a later session write without an earlier write from the same session.
    /// </summary>
    MonotonicWrites,

    /// <summary>
    /// A replica exposed a write without a version that the writing session had previously read.
    /// </summary>
    WritesFollowReads,

    /// <summary>
    /// A replica exposed a version without its complete causal dependency closure.
    /// </summary>
    CausalVisibility,

    /// <summary>
    /// A read disagreed with the explicitly recorded replica snapshot.
    /// </summary>
    ReplicaReadAgreement,

    /// <summary>
    /// Replicas failed to converge before the configured virtual-time deadline.
    /// </summary>
    Convergence
}
