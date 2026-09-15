namespace Causalia.Consistency;

/// <summary>
/// Represents a deterministic distributed-consistency violation.
/// </summary>
public sealed class SimulationConsistencyViolationException : Exception
{
    internal SimulationConsistencyViolationException(string historyName, ConsistencyViolationKind kind, string message, string? sessionId, string? replicaId, object? key)
        : base(message)
    {
        HistoryName = historyName;
        Kind = kind;
        SessionId = sessionId;
        ReplicaId = replicaId;
        Key = key;
    }

    /// <summary>
    /// Gets the logical consistency history that failed.
    /// </summary>
    public string HistoryName { get; }

    /// <summary>
    /// Gets the violated consistency rule.
    /// </summary>
    public ConsistencyViolationKind Kind { get; }

    /// <summary>
    /// Gets the related session identifier when the violation is session-specific.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Gets the related replica identifier when the violation is replica-specific.
    /// </summary>
    public string? ReplicaId { get; }

    /// <summary>
    /// Gets the related logical key when the violation is key-specific.
    /// </summary>
    public object? Key { get; }
}
