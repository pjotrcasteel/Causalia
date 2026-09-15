namespace Causalia.Consistency;

/// <summary>
/// Describes one successfully completed bounded replica-convergence requirement.
/// </summary>
public sealed class ConsistencyConvergenceOutcome
{
    internal ConsistencyConvergenceOutcome(string name, IReadOnlyList<string> replicas, DateTimeOffset registeredAt, DateTimeOffset completedAt)
    {
        Name = name;
        Replicas = Array.AsReadOnly(replicas.ToArray());
        RegisteredAt = registeredAt;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Gets the requirement name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the replicas that were required to converge.
    /// </summary>
    public IReadOnlyList<string> Replicas { get; }

    /// <summary>
    /// Gets the virtual timestamp at which the requirement was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; }

    /// <summary>
    /// Gets the virtual timestamp at which all selected replicas agreed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; }
}
