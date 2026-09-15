using Causalia.Invariants;

namespace Causalia.Exceptions;

/// <summary>
/// Indicates that an invariant predicate threw while Causalia evaluated it.
/// </summary>
public sealed class SimulationInvariantEvaluationException : SimulationInvariantException
{
    internal SimulationInvariantEvaluationException(
        string invariantName,
        InvariantKind kind,
        DateTimeOffset observedAt,
        Exception innerException)
        : base(
            $"Invariant '{invariantName}' threw while being evaluated at virtual time {observedAt:O}.",
            invariantName,
            kind,
            observedAt,
            innerException)
    {
    }
}
