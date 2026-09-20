namespace Causalia.Tool.Inspection;

internal sealed class BoundaryCandidate
{
    public required string Category { get; init; }

    public required string Name { get; init; }

    public required string FilePath { get; init; }

    public required int Line { get; init; }

    public required string Reason { get; init; }

    public required int Score { get; init; }

    public required string SuggestedScenario { get; init; }
}
