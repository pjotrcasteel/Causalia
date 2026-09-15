namespace Causalia.Linearizability;

/// <summary>
/// Describes the outcome of a bounded linearizability check.
/// </summary>
public enum LinearizabilityStatus
{
    /// <summary>
    /// A valid sequential explanation was found.
    /// </summary>
    Linearizable,

    /// <summary>
    /// All reachable sequential explanations were rejected.
    /// </summary>
    NotLinearizable,

    /// <summary>
    /// The configured search bounds were reached before a conclusion could be proven.
    /// </summary>
    Inconclusive
}
