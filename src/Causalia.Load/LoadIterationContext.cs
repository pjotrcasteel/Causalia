namespace Causalia.Load;

/// <summary>
/// Describes one logical iteration in a deterministic load run.
/// </summary>
public sealed class LoadIterationContext
{
    internal LoadIterationContext(
        SimulationContext simulation,
        long iterationId,
        int actorId,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        Simulation = simulation;
        IterationId = iterationId;
        ActorId = actorId;
        ScheduledAt = scheduledAt;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the simulation context that owns this load run.
    /// </summary>
    public SimulationContext Simulation { get; }

    /// <summary>
    /// Gets the stable logical iteration identifier.
    /// </summary>
    public long IterationId { get; }

    /// <summary>
    /// Gets the stable logical actor identifier for this iteration.
    /// </summary>
    public int ActorId { get; }

    /// <summary>
    /// Gets the virtual instant at which this iteration was scheduled to start.
    /// </summary>
    public DateTimeOffset ScheduledAt { get; }

    /// <summary>
    /// Gets the cancellation token for this load iteration.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
