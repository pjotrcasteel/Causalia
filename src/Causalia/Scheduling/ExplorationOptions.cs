namespace Causalia.Scheduling;

/// <summary>
/// Configures bounded systematic exploration of deterministic scheduler choices.
/// </summary>
public sealed class ExplorationOptions
{
    /// <summary>
    /// Gets or initializes the exploration strategy.
    /// </summary>
    public ExplorationStrategy Strategy { get; init; } = ExplorationStrategy.DepthFirst;

    /// <summary>
    /// Gets or initializes the simulation settings reused for every explored schedule.
    /// </summary>
    public SimulationOptions Simulation { get; init; } = new();

    /// <summary>
    /// Gets or initializes the maximum number of distinct schedules explored in one call.
    /// </summary>
    public int MaxSchedules { get; init; } = 1_000;

    /// <summary>
    /// Gets or initializes the maximum number of branching decisions that are systematically backtracked.
    /// </summary>
    public int MaxDecisionDepth { get; init; } = 100;

    /// <summary>
    /// Gets or initializes the maximum number of scheduler preemptions explored by DPOR, or null for no preemption bound.
    /// </summary>
    public int? MaxPreemptions { get; init; }
}
