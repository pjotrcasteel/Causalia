using System.Text;

namespace Causalia.Tool.Initialization;

internal static class InitializationResultWriter
{
    public static async Task WriteAsync(
        InitializationResult result,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(writer);

        var builder = new StringBuilder();
        builder.AppendLine("Causalia initialization complete");
        builder.AppendLine("===============================");
        builder.AppendLine($"Target project: {result.TargetProject}");
        builder.AppendLine($"Test project:   {result.TestProject}");
        builder.AppendLine($"First test:     {result.TestFile}");
        builder.AppendLine();
        builder.AppendLine("Packages:");

        foreach (var package in result.PackageReferences)
        {
            builder.AppendLine($"  {package}");
        }

        builder.AppendLine();

        if (result.AddedToSolution)
        {
            builder.AppendLine($"Added to solution: {result.SolutionPath}");
        }
        else if (result.SolutionPath is not null)
        {
            builder.AppendLine($"Solution was not modified automatically: {result.SolutionPath}");
        }

        if (result.SuggestedBoundary is not null)
        {
            builder.AppendLine();
            builder.AppendLine($"Suggested boundary: {result.SuggestedBoundary}");
            builder.AppendLine($"First real scenario: {result.SuggestedScenario}");
        }

        builder.AppendLine();
        builder.AppendLine($"Run: dotnet test \"{result.TestProject}\"");

        await writer.WriteAsync(builder.ToString().TrimEnd().AsMemory(), cancellationToken);
    }
}
