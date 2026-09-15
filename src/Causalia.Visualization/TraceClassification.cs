namespace Causalia.Visualization;

internal sealed record TraceClassification(
    TraceEventCategory Category,
    TraceEventSeverity Severity,
    string Lane,
    string? CorrelationId);
