namespace Causalia.Tool.Inspection;

internal sealed class FindingDefinition
{
    public required string Marker { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }

    public required string Recommendation { get; init; }

    public string? AnalyzerRuleId { get; init; }
}
