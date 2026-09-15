namespace Causalia.Invariants;

/// <summary>
/// Describes one invariant that completed successfully during a simulation run.
/// </summary>
public sealed class InvariantOutcome
{
    internal InvariantOutcome(string name, InvariantKind kind, DateTimeOffset registeredAt, DateTimeOffset completedAt)
    {
        Name = name;
        Kind = kind;
        RegisteredAt = registeredAt;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Gets the stable user-defined invariant name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the invariant temporal semantics.
    /// </summary>
    public InvariantKind Kind { get; }

    /// <summary>
    /// Gets the virtual instant at which the invariant was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; }

    /// <summary>
    /// Gets the virtual instant at which the invariant obligation completed successfully.
    /// </summary>
    public DateTimeOffset CompletedAt { get; }
}
