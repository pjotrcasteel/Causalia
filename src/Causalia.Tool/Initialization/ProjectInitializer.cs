using Causalia.Tool.Inspection;

namespace Causalia.Tool.Initialization;

internal sealed class ProjectInitializer
{
    private readonly InspectionTargetResolver _targetResolver = new();
    private readonly ProjectInspector _projectInspector = new();
    private readonly ProjectTemplateWriter _templateWriter = new();
    private readonly SolutionProjectAdder _solutionProjectAdder = new();

    public async Task<InitializationResult> InitializeAsync(
        string targetPath,
        string? projectPath,
        bool force,
        CancellationToken cancellationToken)
    {
        var target = await _targetResolver.ResolveAsync(targetPath, cancellationToken);
        var report = await _projectInspector.InspectAsync(targetPath, cancellationToken);
        var selectedProject = ProjectSelection.Select(report, target.RootDirectory, projectPath);
        var selectedProjectPath = Path.GetFullPath(Path.Combine(target.RootDirectory, selectedProject.Path));
        var projectReport = await _projectInspector.InspectAsync(selectedProjectPath, cancellationToken);
        var definition = GeneratedProjectFactory.Create(
            target.RootDirectory,
            selectedProject,
            projectReport.RecommendedPackages);
        var boundary = projectReport.SuggestedStartingPoint;

        await _templateWriter.WriteAsync(
            definition,
            FormatBoundary(boundary),
            boundary?.SuggestedScenario,
            force,
            cancellationToken);

        var solutionPath = InitializationTargetResolver.FindSolutionPath(targetPath, target.RootDirectory);
        var addedToSolution = await _solutionProjectAdder.TryAddAsync(
            solutionPath,
            target.RootDirectory,
            definition.TestProjectPath,
            cancellationToken);

        return new InitializationResult
        {
            TargetProject = selectedProject.Path,
            TestProject = Path.GetRelativePath(target.RootDirectory, definition.TestProjectPath).Replace('\\', '/'),
            TestFile = Path.GetRelativePath(target.RootDirectory, definition.TestFilePath).Replace('\\', '/'),
            PackageReferences = definition.CausaliaPackages,
            AddedToSolution = addedToSolution,
            SolutionPath = solutionPath is null
                ? null
                : Path.GetRelativePath(target.RootDirectory, solutionPath).Replace('\\', '/'),
            SuggestedBoundary = FormatBoundary(boundary),
            SuggestedScenario = boundary?.SuggestedScenario
        };
    }

    private static string? FormatBoundary(BoundaryCandidate? boundary)
    {
        return boundary is null
            ? null
            : $"{boundary.FilePath}:{boundary.Line} {boundary.Name} ({boundary.Category})";
    }
}
