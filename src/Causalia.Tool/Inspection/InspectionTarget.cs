namespace Causalia.Tool.Inspection;

internal sealed class InspectionTarget
{
    public required string RootDirectory { get; init; }

    public required IReadOnlyList<string> ProjectFiles { get; init; }
}
