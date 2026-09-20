namespace Causalia.Tool.Initialization;

internal sealed class InitializationResult
{
    public required string TargetProject { get; init; }

    public required string TestProject { get; init; }

    public required string TestFile { get; init; }

    public required IReadOnlyList<string> PackageReferences { get; init; }

    public required bool AddedToSolution { get; init; }

    public required string? SolutionPath { get; init; }

    public required string? SuggestedBoundary { get; init; }

    public required string? SuggestedScenario { get; init; }
}
