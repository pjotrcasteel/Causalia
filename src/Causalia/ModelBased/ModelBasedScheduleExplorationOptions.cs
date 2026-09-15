using Causalia.Scheduling;

namespace Causalia.ModelBased;

/// <summary>
/// Configures scheduler exploration performed for every explored model command sequence.
/// </summary>
public sealed class ModelBasedScheduleExplorationOptions
{
    /// <summary>
    /// Gets or initializes the scheduler exploration strategy.
    /// </summary>
    public ExplorationStrategy Strategy { get; init; } = ExplorationStrategy.DynamicPartialOrderReduction;

    /// <summary>
    /// Gets or initializes the maximum schedules explored for one model sequence.
    /// </summary>
    public int MaxSchedules { get; init; } = 1_000;

    /// <summary>
    /// Gets or initializes the maximum scheduler decision depth systematically backtracked for one model sequence.
    /// </summary>
    public int MaxDecisionDepth { get; init; } = 100;

    /// <summary>
    /// Gets or initializes the optional DPOR preemption bound.
    /// </summary>
    public int? MaxPreemptions { get; init; }
}
