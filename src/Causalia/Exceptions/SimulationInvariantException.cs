using Causalia.Invariants;

namespace Causalia.Exceptions;

/// <summary>
/// Base exception for failures raised while evaluating deterministic simulation invariants.
/// </summary>
public abstract class SimulationInvariantException : Exception
{
    private protected SimulationInvariantException(
        string message,
        string invariantName,
        InvariantKind kind,
        DateTimeOffset observedAt,
        Exception? innerException = null)
        : base(message, innerException)
    {
        InvariantName = invariantName;
        Kind = kind;
        ObservedAt = observedAt;
    }

    /// <summary>
    /// Gets the stable user-defined invariant name.
    /// </summary>
    public string InvariantName { get; }

    /// <summary>
    /// Gets the temporal semantics of the failed invariant.
    /// </summary>
    public InvariantKind Kind { get; }

    /// <summary>
    /// Gets the virtual instant at which the failure was observed.
    /// </summary>
    public DateTimeOffset ObservedAt { get; }
}
