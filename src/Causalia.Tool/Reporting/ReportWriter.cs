using System.Text;
using System.Text.Json;
using Causalia.Tool.Inspection;

namespace Causalia.Tool.Reporting;

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task WriteAsync(
        InspectionReport report,
        OutputFormat outputFormat,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        var output = outputFormat == OutputFormat.Json
            ? JsonSerializer.Serialize(report, JsonOptions)
            : BuildText(report);

        await writer.WriteAsync(output.AsMemory(), cancellationToken);
    }

    private static string BuildText(InspectionReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Causalia project inspection");
        builder.AppendLine("===========================");
        builder.AppendLine($"Target: {report.Target}");
        builder.AppendLine($"Projects: {report.Projects.Count}");
        builder.AppendLine($"Source files scanned: {report.SourceFilesScanned}");
        builder.AppendLine();

        AppendValues(builder, "Detected", report.DetectedTechnologies, "No framework-specific integrations detected.");
        AppendAdoptionReadiness(builder, report.AdoptionCapabilities);
        AppendBoundaries(builder, report.BoundaryCandidates);
        AppendFindings(builder, report.Findings);
        AppendSuggestedStart(builder, report.SuggestedStartingPoint);
        AppendValues(builder, "Recommended packages", report.RecommendedPackages, "Causalia");

        return builder.ToString().TrimEnd();
    }

    private static void AppendValues(
        StringBuilder builder,
        string title,
        IReadOnlyList<string> values,
        string emptyValue)
    {
        AppendHeader(builder, title);

        if (values.Count == 0)
        {
            builder.AppendLine(emptyValue);
        }
        else
        {
            foreach (var value in values)
            {
                builder.AppendLine(value);
            }
        }

        builder.AppendLine();
    }

    private static void AppendAdoptionReadiness(StringBuilder builder, AdoptionCapabilities capabilities)
    {
        AppendHeader(builder, "Adoption readiness");
        builder.AppendLine($"TimeProvider: {FormatDetected(capabilities.UsesTimeProvider)}");
        builder.AppendLine($"CancellationToken flow: {FormatDetected(capabilities.UsesCancellationToken)}");
        builder.AppendLine($"Retry-shaped code: {FormatDetected(capabilities.UsesRetryMechanisms)}");
        builder.AppendLine();
    }

    private static string FormatDetected(bool value)
    {
        return value ? "detected" : "not detected";
    }

    private static void AppendBoundaries(StringBuilder builder, IReadOnlyList<BoundaryCandidate> candidates)
    {
        AppendHeader(builder, "Simulation boundaries");

        if (candidates.Count == 0)
        {
            builder.AppendLine("No obvious boundary was detected. Start from a retrying service method and one dependency seam.");
            builder.AppendLine();
            return;
        }

        foreach (var candidate in candidates.Take(12))
        {
            builder.AppendLine($"[{candidate.Category}] {candidate.FilePath}:{candidate.Line} {candidate.Name}");
            builder.AppendLine($"  {candidate.Reason}");
        }

        if (candidates.Count > 12)
        {
            builder.AppendLine($"... {candidates.Count - 12} additional candidates omitted.");
        }

        builder.AppendLine();
    }

    private static void AppendFindings(StringBuilder builder, IReadOnlyList<SourceFinding> findings)
    {
        AppendHeader(builder, "Determinism candidates");

        if (findings.Count == 0)
        {
            builder.AppendLine("No obvious wall-clock, random, identifier, or scheduler escape candidates detected.");
            builder.AppendLine();
            return;
        }

        foreach (var finding in findings.Take(20))
        {
            builder.AppendLine($"{finding.Code} {finding.FilePath}:{finding.Line}");
            builder.AppendLine($"  {finding.Description}");

            if (finding.AnalyzerRuleId is not null)
            {
                builder.AppendLine($"  Compiler rule in deterministic code: {finding.AnalyzerRuleId}");
            }

            builder.AppendLine($"  {finding.Recommendation}");
        }

        if (findings.Count > 20)
        {
            builder.AppendLine($"... {findings.Count - 20} additional findings omitted. Use --format json for the complete report.");
        }

        builder.AppendLine();
    }

    private static void AppendSuggestedStart(StringBuilder builder, BoundaryCandidate? candidate)
    {
        AppendHeader(builder, "Suggested starting point");

        if (candidate is null)
        {
            builder.AppendLine("Pick one service method that retries or writes durable state, then simulate one ambiguous outcome.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine($"{candidate.FilePath}:{candidate.Line} {candidate.Name}");
        builder.AppendLine($"Boundary: {candidate.Category}");
        builder.AppendLine($"Why: {candidate.Reason}");
        builder.AppendLine($"First scenario: {candidate.SuggestedScenario}");
        builder.AppendLine();
    }

    private static void AppendHeader(StringBuilder builder, string title)
    {
        builder.AppendLine(title);
        builder.AppendLine(new string('-', title.Length));
    }
}
