namespace Causalia.Tool.Inspection;

internal sealed class SourceInspectionResult
{
    public required IReadOnlyList<SourceFinding> Findings { get; init; }

    public required IReadOnlyList<BoundaryCandidate> BoundaryCandidates { get; init; }

    public required AdoptionCapabilities AdoptionCapabilities { get; init; }

    public required int SourceFilesScanned { get; init; }
}
