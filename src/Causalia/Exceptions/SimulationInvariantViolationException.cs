using Causalia.Invariants;

namespace Causalia.Exceptions;

/// <summary>
/// Indicates that a deterministic simulation invariant was violated.
/// </summary>
public sealed class SimulationInvariantViolationException : SimulationInvariantException
{
    internal SimulationInvariantViolationException(
        string invariantName,
        InvariantKind kind,
        DateTimeOffset observedAt,
        DateTimeOffset? deadline)
        : base(CreateMessage(invariantName, kind, observedAt, deadline), invariantName, kind, observedAt)
    {
        Deadline = deadline;
    }

    /// <summary>
    /// Gets the liveness deadline for an eventually invariant, or <see langword="null"/> for safety invariants.
    /// </summary>
    public DateTimeOffset? Deadline { get; }

    private static string CreateMessage(
        string invariantName,
        InvariantKind kind,
        DateTimeOffset observedAt,
        DateTimeOffset? deadline)
    {
        return kind switch
        {
            InvariantKind.Always => $"Invariant '{invariantName}' expected its condition to always remain true at {observedAt:O}.",
            InvariantKind.Never => $"Invariant '{invariantName}' expected its condition to never become true at {observedAt:O}.",
            InvariantKind.Eventually =>
                $"Invariant '{invariantName}' was not satisfied by its virtual deadline {deadline:O}. Observed at {observedAt:O}.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported invariant kind.")
        };
    }
}
