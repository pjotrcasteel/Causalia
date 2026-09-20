namespace Causalia.Tool.Initialization;

internal sealed class GeneratedProjectDefinition
{
    public required string RootDirectory { get; init; }

    public required string TargetProjectPath { get; init; }

    public required string TestProjectDirectory { get; init; }

    public required string TestProjectPath { get; init; }

    public required string TestFilePath { get; init; }

    public required string Namespace { get; init; }

    public required IReadOnlyList<string> CausaliaPackages { get; init; }
}
