namespace Causalia.Tool.Inspection;

internal sealed class SourceFinding
{
    public required string Code { get; init; }

    public required string Description { get; init; }

    public required string FilePath { get; init; }

    public required int Line { get; init; }

    public required string Snippet { get; init; }

    public required string Recommendation { get; init; }

    public string? AnalyzerRuleId { get; init; }
}
