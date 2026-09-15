namespace Causalia.Runtime;

internal sealed class ExplorationDecisionMetadata
{
    public required int DecisionIndex { get; init; }

    public required IReadOnlyList<long?> CandidateOperationIds { get; init; }

    public required int SelectedIndex { get; init; }
}
