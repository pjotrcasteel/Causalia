namespace Causalia.Tool.Inspection;

internal sealed class BoundaryDefinition
{
    public required string Category { get; init; }

    public required int Score { get; init; }

    public required string Reason { get; init; }

    public required string SuggestedScenario { get; init; }

    public required IReadOnlyList<string> Tokens { get; init; }
}
