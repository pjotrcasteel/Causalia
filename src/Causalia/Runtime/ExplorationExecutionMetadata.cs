namespace Causalia.Runtime;

internal sealed class ExplorationExecutionMetadata
{
    public required IReadOnlyList<ExplorationDecisionMetadata> Decisions { get; init; }

    public required IReadOnlyDictionary<long, ExplorationOperationRegistration> Operations { get; init; }
}
