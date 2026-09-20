namespace Causalia.Tool.Inspection;

internal sealed class ProjectSummary
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string ProjectDirectory { get; init; }

    public required string Sdk { get; init; }

    public required IReadOnlyList<string> TargetFrameworks { get; init; }

    public required IReadOnlyList<string> PackageReferences { get; init; }

    public required IReadOnlyList<string> FrameworkReferences { get; init; }

    public bool IsTestProject { get; init; }
}
