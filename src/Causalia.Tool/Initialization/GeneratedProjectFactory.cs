using Causalia.Tool.Inspection;

namespace Causalia.Tool.Initialization;

internal static class GeneratedProjectFactory
{
    public static GeneratedProjectDefinition Create(
        string rootDirectory,
        ProjectSummary selectedProject,
        IReadOnlyList<string> recommendedPackages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(selectedProject);
        ArgumentNullException.ThrowIfNull(recommendedPackages);

        EnsureNet10(selectedProject);
        var projectName = selectedProject.Name + ".Causalia.Tests";
        var targetProjectPath = Path.GetFullPath(Path.Combine(rootDirectory, selectedProject.Path));
        var testDirectory = Path.Combine(rootDirectory, "tests", projectName);
        var relativeTestDirectory = Path.GetRelativePath(Path.GetDirectoryName(targetProjectPath)!, testDirectory);

        if (!Path.IsPathRooted(relativeTestDirectory)
            && relativeTestDirectory != ".."
            && !relativeTestDirectory.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            // SDK default items exclude dot-directories, keeping generated tests out of the production compilation.
            testDirectory = Path.Combine(rootDirectory, ".causalia", "tests", projectName);
        }

        var packages = recommendedPackages
            .Where(package => package.StartsWith("Causalia", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(package => package, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GeneratedProjectDefinition
        {
            RootDirectory = rootDirectory,
            TargetProjectPath = targetProjectPath,
            TestProjectDirectory = testDirectory,
            TestProjectPath = Path.Combine(testDirectory, projectName + ".csproj"),
            TestFilePath = Path.Combine(testDirectory, "GoldenPathSimulationTests.cs"),
            Namespace = SanitizeNamespace(projectName),
            CausaliaPackages = packages.AsReadOnly()
        };
    }

    private static void EnsureNet10(ProjectSummary project)
    {
        if (!project.TargetFrameworks.Contains("net10.0", StringComparer.OrdinalIgnoreCase))
        {
            var frameworks = project.TargetFrameworks.Count == 0
                ? "none detected"
                : string.Join(", ", project.TargetFrameworks);
            throw new ArgumentException(
                $"Project '{project.Path}' targets {frameworks}. Causalia runtime packages currently require net10.0.");
        }
    }

    private static string SanitizeNamespace(string value)
    {
        return string.Join(
            ".",
            value.Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeSegment));
    }

    private static string SanitizeSegment(string value)
    {
        var characters = value
            .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')
            .ToArray();
        var result = new string(characters);

        if (result.Length == 0)
        {
            return "Generated";
        }

        return char.IsDigit(result[0]) ? "_" + result : result;
    }
}
