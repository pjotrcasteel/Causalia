namespace Causalia.Tool.Inspection;

internal sealed class ProjectInspector
{
    private readonly InspectionTargetResolver _targetResolver = new();
    private readonly ProjectFileReader _projectFileReader = new();
    private readonly SourceInspector _sourceInspector = new();
    private readonly TechnologyDetector _technologyDetector = new();
    private readonly PackageRecommender _packageRecommender = new();

    public async Task<InspectionReport> InspectAsync(string targetPath, CancellationToken cancellationToken)
    {
        var target = await _targetResolver.ResolveAsync(targetPath, cancellationToken);
        var projects = new List<ProjectSummary>();

        foreach (var projectFile in target.ProjectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            projects.Add(await _projectFileReader.ReadAsync(projectFile, target.RootDirectory, cancellationToken));
        }

        var sourceInspection = await _sourceInspector.InspectAsync(target.RootDirectory, projects.AsReadOnly(), cancellationToken);
        var technologies = _technologyDetector.Detect(projects.AsReadOnly(), sourceInspection.BoundaryCandidates);
        var recommendedPackages = _packageRecommender.Recommend(technologies, sourceInspection.BoundaryCandidates);

        return new InspectionReport
        {
            Target = Path.GetFullPath(targetPath),
            Projects = projects.AsReadOnly(),
            SourceFilesScanned = sourceInspection.SourceFilesScanned,
            DetectedTechnologies = technologies,
            AdoptionCapabilities = sourceInspection.AdoptionCapabilities,
            Findings = sourceInspection.Findings,
            BoundaryCandidates = sourceInspection.BoundaryCandidates,
            RecommendedPackages = recommendedPackages,
            SuggestedStartingPoint = sourceInspection.BoundaryCandidates.FirstOrDefault()
        };
    }
}
