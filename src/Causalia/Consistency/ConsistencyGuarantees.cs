namespace Causalia.Consistency;

/// <summary>
/// Describes distributed-consistency guarantees that Causalia can verify from recorded logical observations.
/// </summary>
[Flags]
public enum ConsistencyGuarantees
{
    /// <summary>
    /// Does not enable any consistency verification.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requires a session to observe its own previous writes, or a causally newer version, on later reads of the same key.
    /// </summary>
    ReadYourWrites = 1,

    /// <summary>
    /// Requires later reads in one session to never move behind an earlier observed version of the same key.
    /// </summary>
    MonotonicReads = 2,

    /// <summary>
    /// Requires replicas that expose a later write from one session to also expose every earlier write from that session.
    /// </summary>
    MonotonicWrites = 4,

    /// <summary>
    /// Requires replicas that expose a write to also expose the versions read by that session before the write.
    /// </summary>
    WritesFollowReads = 8,

    /// <summary>
    /// Requires every observed replica version to include the complete causal dependency closure of that version.
    /// </summary>
    CausalVisibility = 16,

    /// <summary>
    /// Requires a recorded read to agree with the latest explicitly observed snapshot for that replica.
    /// </summary>
    ReplicaReadAgreement = 32,

    /// <summary>
    /// Enables all four client-centric session guarantees.
    /// </summary>
    Session = ReadYourWrites | MonotonicReads | MonotonicWrites | WritesFollowReads,

    /// <summary>
    /// Enables the session guarantees plus causal visibility across replica snapshots.
    /// </summary>
    Causal = Session | CausalVisibility
}
