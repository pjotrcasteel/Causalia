namespace Causalia.ModelBased;

/// <summary>
/// Describes a completed bounded model-state exploration.
/// </summary>
public sealed class ModelBasedExplorationResult
{
    internal ModelBasedExplorationResult(ModelBasedExplorationResultData data)
    {
        SequencesExplored = data.SequencesExplored;
        CommandsExecuted = data.CommandsExecuted;
        SchedulesExplored = data.SchedulesExplored;
        MaximumDepthReached = data.MaximumDepthReached;
        ExhaustedWithinBounds = data.ExhaustedWithinBounds;
        DepthLimitReached = data.DepthLimitReached;
    }

    /// <summary>
    /// Gets the number of non-empty model command sequences executed.
    /// </summary>
    public int SequencesExplored { get; }

    /// <summary>
    /// Gets the total number of model commands executed across all sequence runs and schedule variants.
    /// </summary>
    public long CommandsExecuted { get; }

    /// <summary>
    /// Gets the total number of deterministic scheduler timelines executed across all model sequences.
    /// </summary>
    public int SchedulesExplored { get; }

    /// <summary>
    /// Gets the greatest model command depth reached by an executed sequence.
    /// </summary>
    public int MaximumDepthReached { get; }

    /// <summary>
    /// Gets whether every reachable command sequence within the configured command-depth bound was explored.
    /// </summary>
    public bool ExhaustedWithinBounds { get; }

    /// <summary>
    /// Gets whether exploration reached the command-depth bound while more model commands remained enabled.
    /// </summary>
    public bool DepthLimitReached { get; }
}
