namespace Causalia.Runtime;

internal sealed class DporAnalysisResult
{
    public required IReadOnlyList<IReadOnlyList<int>> AlternativePrefixes { get; init; }

    public required int EquivalentSchedulesPruned { get; init; }

    public required int PreemptionBoundPruned { get; init; }

    public required int PreemptionsObserved { get; init; }
}
