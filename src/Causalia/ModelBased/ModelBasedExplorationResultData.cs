namespace Causalia.ModelBased;

internal sealed class ModelBasedExplorationResultData
{
    public required int SequencesExplored { get; init; }

    public required long CommandsExecuted { get; init; }

    public required int SchedulesExplored { get; init; }

    public required int MaximumDepthReached { get; init; }

    public required bool ExhaustedWithinBounds { get; init; }

    public required bool DepthLimitReached { get; init; }
}
