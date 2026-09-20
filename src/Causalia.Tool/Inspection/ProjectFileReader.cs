using System.Xml.Linq;

namespace Causalia.Tool.Inspection;

internal sealed class ProjectFileReader
{
    public async Task<ProjectSummary> ReadAsync(
        string projectPath,
        string rootDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        await using var stream = File.OpenRead(projectPath);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var root = document.Root ?? throw new InvalidDataException($"Project '{projectPath}' has no XML root element.");
        var targetFrameworks = ReadTargetFrameworks(root);
        var packageReferences = ReadReferences(root, "PackageReference");
        var frameworkReferences = ReadReferences(root, "FrameworkReference");
        var projectDirectory = Path.GetDirectoryName(projectPath)!;

        return new ProjectSummary
        {
            Name = Path.GetFileNameWithoutExtension(projectPath),
            Path = ToRelativePath(rootDirectory, projectPath),
            ProjectDirectory = projectDirectory,
            Sdk = root.Attribute("Sdk")?.Value ?? string.Empty,
            TargetFrameworks = targetFrameworks,
            PackageReferences = packageReferences,
            FrameworkReferences = frameworkReferences,
            IsTestProject = IsTestProject(root, packageReferences, projectPath)
        };
    }

    private static IReadOnlyList<string> ReadTargetFrameworks(XElement root)
    {
        var values = root
            .Descendants()
            .Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(element => (element.Value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.AsReadOnly();
    }

    private static IReadOnlyList<string> ReadReferences(XElement root, string elementName)
    {
        var values = root
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, elementName, StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.AsReadOnly();
    }

    private static bool IsTestProject(XElement root, IReadOnlyList<string> packages, string projectPath)
    {
        var explicitValue = root
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "IsTestProject", StringComparison.Ordinal))
            ?.Value;

        if (bool.TryParse(explicitValue, out var isTestProject) && isTestProject)
        {
            return true;
        }

        if (packages.Any(IsTestPackage))
        {
            return true;
        }

        return projectPath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "tests", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTestPackage(string package)
    {
        return package.StartsWith("MSTest.", StringComparison.OrdinalIgnoreCase)
            || package.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
            || package.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(package, "Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToRelativePath(string rootDirectory, string path)
    {
        return Path.GetRelativePath(rootDirectory, path).Replace('\\', '/');
    }
}
