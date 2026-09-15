using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Causalia.Visualization;

/// <summary>
/// Exports Causalia trace documents as self-contained interactive HTML.
/// </summary>
public static class SimulationTraceHtmlExporter
{
    private const string TemplateResourceName = "Causalia.Visualization.TraceViewerTemplate.html";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Creates a complete self-contained HTML trace viewer.
    /// </summary>
    public static string Export(SimulationTraceDocument document, TraceHtmlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new TraceHtmlOptions();
        ValidateOptions(options);
        var exportEvents = document.Events.Take(options.MaxEvents).ToList().AsReadOnly();
        var payload = CreatePayload(document, exportEvents);
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var template = LoadTemplate();
        return template
            .Replace("{{TITLE}}", EscapeHtml(options.Title), StringComparison.Ordinal)
            .Replace("{{SHOW_SCHEDULER}}", options.ShowSchedulerEvents ? "true" : "false", StringComparison.Ordinal)
            .Replace("{{TRACE_JSON}}", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes a complete self-contained HTML trace viewer to a file.
    /// </summary>
    public static async Task ExportAsync(
        SimulationTraceDocument document,
        string path,
        TraceHtmlOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var html = Export(document, options);
        await File.WriteAllTextAsync(path, html, cancellationToken);
    }

    private static string LoadTemplate()
    {
        var assembly = typeof(SimulationTraceHtmlExporter).Assembly;
        using var stream = assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException($"Embedded trace viewer template '{TemplateResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void ValidateOptions(TraceHtmlOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Title);

        if (options.MaxEvents <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxEvents, "MaxEvents must be greater than zero.");
        }
    }

    private static string EscapeHtml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    private static TraceHtmlPayload CreatePayload(
        SimulationTraceDocument document,
        IReadOnlyList<VisualTraceEvent> exportEvents)
    {
        var summary = new TraceHtmlSummary
        {
            Seed = document.Summary.Seed,
            Steps = document.Summary.Steps,
            VirtualElapsedTicks = document.Summary.VirtualElapsed.Ticks,
            EventCount = document.Summary.EventCount,
            FaultCount = document.Summary.FaultCount,
            TimeTravelCheckpointCount = document.Summary.TimeTravelCheckpointCount,
            DroppedTimeTravelCheckpointCount = document.Summary.DroppedTimeTravelCheckpointCount,
            ReplayToken = document.Summary.ReplayToken,
            FailureType = document.Summary.FailureType,
            FailureMessage = document.Summary.FailureMessage,
            FailureSignature = document.Summary.FailureSignature,
            FailureKind = document.Summary.FailureKind,
            FailureTrigger = document.Summary.FailureTrigger,
            FailureConfidence = document.Summary.FailureConfidence,
            ReproductionToken = document.Summary.ReproductionToken
        };
        var lanes = document.Lanes
            .Select(
                lane => new TraceHtmlLane
                {
                    Name = lane.Name,
                    EventCount = lane.EventCount
                })
            .ToList()
            .AsReadOnly();
        var events = exportEvents
            .Select(
                traceEvent => new TraceHtmlEvent
                {
                    Index = traceEvent.Index,
                    Step = traceEvent.Step,
                    Timestamp = traceEvent.Timestamp,
                    ElapsedTicks = traceEvent.Elapsed.Ticks,
                    Message = traceEvent.Message,
                    Category = traceEvent.Category,
                    Severity = traceEvent.Severity,
                    Lane = traceEvent.Lane,
                    CorrelationId = traceEvent.CorrelationId
                })
            .ToList()
            .AsReadOnly();
        return new TraceHtmlPayload
        {
            Summary = summary,
            Lanes = lanes,
            Events = events,
            TimeTravel = document.TimeTravel,
            Truncated = document.Events.Count > exportEvents.Count
        };
    }

}
