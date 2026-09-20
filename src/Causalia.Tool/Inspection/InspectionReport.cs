namespace Causalia.Tool.Inspection;

internal sealed class InspectionReport
{
    public required string Target { get; init; }

    public required IReadOnlyList<ProjectSummary> Projects { get; init; }

    public required int SourceFilesScanned { get; init; }

    public required IReadOnlyList<string> DetectedTechnologies { get; init; }

    public required AdoptionCapabilities AdoptionCapabilities { get; init; }

    public required IReadOnlyList<SourceFinding> Findings { get; init; }

    public required IReadOnlyList<BoundaryCandidate> BoundaryCandidates { get; init; }

    public required IReadOnlyList<string> RecommendedPackages { get; init; }

    public BoundaryCandidate? SuggestedStartingPoint { get; init; }
}
