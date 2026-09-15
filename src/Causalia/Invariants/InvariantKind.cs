namespace Causalia.Invariants;

/// <summary>
/// Identifies the temporal semantics of a simulation invariant.
/// </summary>
public enum InvariantKind
{
    /// <summary>
    /// Requires the invariant predicate to remain true for every deterministic observation point after registration.
    /// </summary>
    Always,

    /// <summary>
    /// Requires the invariant predicate to remain false for every deterministic observation point after registration.
    /// </summary>
    Never,

    /// <summary>
    /// Requires the invariant predicate to become true within its configured virtual-time deadline.
    /// </summary>
    Eventually
}
