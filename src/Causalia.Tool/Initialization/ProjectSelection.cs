using Causalia.Tool.Inspection;

namespace Causalia.Tool.Initialization;

internal static class ProjectSelection
{
    public static ProjectSummary Select(
        InspectionReport report,
        string rootDirectory,
        string? requestedProjectPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (!string.IsNullOrWhiteSpace(requestedProjectPath))
        {
            return SelectRequested(report.Projects, rootDirectory, requestedProjectPath);
        }

        var productionProjects = report.Projects.Where(project => !project.IsTestProject).ToList();

        if (productionProjects.Count == 0)
        {
            throw new ArgumentException("No production project was found. Specify one with --project.");
        }

        if (productionProjects.Count == 1)
        {
            return productionProjects[0];
        }

        return productionProjects
            .OrderByDescending(project => CountBoundaries(project, rootDirectory, report.BoundaryCandidates))
            .ThenBy(project => project.Path, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static ProjectSummary SelectRequested(
        IReadOnlyList<ProjectSummary> projects,
        string rootDirectory,
        string requestedProjectPath)
    {
        var fullPath = Path.GetFullPath(
            Path.IsPathRooted(requestedProjectPath)
                ? requestedProjectPath
                : Path.Combine(rootDirectory, requestedProjectPath));

        var selected = projects.FirstOrDefault(
            project => string.Equals(
                Path.GetFullPath(Path.Combine(rootDirectory, project.Path)),
                fullPath,
                StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            throw new ArgumentException($"Project '{requestedProjectPath}' is not part of the inspected target.");
        }

        if (selected.IsTestProject)
        {
            throw new ArgumentException($"Project '{requestedProjectPath}' is a test project. Select a production project.");
        }

        return selected;
    }

    private static int CountBoundaries(
        ProjectSummary project,
        string rootDirectory,
        IReadOnlyList<BoundaryCandidate> boundaries)
    {
        var projectDirectory = Path.GetRelativePath(rootDirectory, project.ProjectDirectory).Replace('\\', '/').Trim('/');

        if (projectDirectory.Length == 0 || projectDirectory == ".")
        {
            return boundaries.Count;
        }

        var prefix = projectDirectory + "/";
        return boundaries.Count(
            candidate => candidate.FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
