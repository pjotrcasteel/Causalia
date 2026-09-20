using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Causalia.Tool.Inspection;

internal sealed partial class InspectionTargetResolver
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".causalia",
        ".git",
        ".vs",
        "artifacts",
        "bin",
        "obj",
        "TestResults"
    };

    public async Task<InspectionTarget> ResolveAsync(string targetPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(targetPath);

        if (Directory.Exists(fullPath))
        {
            return ResolveDirectory(fullPath, cancellationToken);
        }

        if (!File.Exists(fullPath))
        {
            throw new ArgumentException($"Inspection target '{targetPath}' does not exist.");
        }

        var extension = Path.GetExtension(fullPath);

        if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return new InspectionTarget
            {
                RootDirectory = Path.GetDirectoryName(fullPath)!,
                ProjectFiles = new List<string> { fullPath }.AsReadOnly()
            };
        }

        if (string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveSlnxAsync(fullPath, cancellationToken);
        }

        if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveSlnAsync(fullPath, cancellationToken);
        }

        throw new ArgumentException("Inspection target must be a directory, .slnx, .sln, or .csproj.");
    }

    private static InspectionTarget ResolveDirectory(string directory, CancellationToken cancellationToken)
    {
        var projects = new List<string>();

        foreach (var project in Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsExcluded(project))
            {
                projects.Add(Path.GetFullPath(project));
            }
        }

        projects.Sort(StringComparer.OrdinalIgnoreCase);
        EnsureProjectsFound(directory, projects);

        return new InspectionTarget
        {
            RootDirectory = directory,
            ProjectFiles = projects.AsReadOnly()
        };
    }

    private static async Task<InspectionTarget> ResolveSlnxAsync(string solutionPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(solutionPath);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var rootDirectory = Path.GetDirectoryName(solutionPath)!;
        var projects = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "Project", StringComparison.Ordinal))
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizeProjectPath(rootDirectory, path!))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        EnsureProjectsFound(solutionPath, projects);

        return new InspectionTarget
        {
            RootDirectory = rootDirectory,
            ProjectFiles = projects.AsReadOnly()
        };
    }

    private static async Task<InspectionTarget> ResolveSlnAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(solutionPath, cancellationToken);
        var rootDirectory = Path.GetDirectoryName(solutionPath)!;
        var projects = new List<string>();

        foreach (Match match in ProjectPathRegex().Matches(content))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(rootDirectory, projectPath));

            if (File.Exists(fullPath) && !projects.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                projects.Add(fullPath);
            }
        }

        projects.Sort(StringComparer.OrdinalIgnoreCase);
        EnsureProjectsFound(solutionPath, projects);

        return new InspectionTarget
        {
            RootDirectory = rootDirectory,
            ProjectFiles = projects.AsReadOnly()
        };
    }

    private static string NormalizeProjectPath(string rootDirectory, string projectPath)
    {
        var normalized = projectPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(rootDirectory, normalized));
    }

    private static bool IsExcluded(string path)
    {
        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(ExcludedDirectories.Contains);
    }

    private static void EnsureProjectsFound(string target, IReadOnlyList<string> projects)
    {
        if (projects.Count == 0)
        {
            throw new ArgumentException($"No .NET project files were found for '{target}'.");
        }
    }

    [GeneratedRegex("\"(?<path>[^\"]+\\.csproj)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProjectPathRegex();
}
