namespace Causalia.Visualization;

internal sealed class TraceHtmlEvent
{
    public int Index { get; init; }

    public int Step { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public long ElapsedTicks { get; init; }

    public string Message { get; init; } = string.Empty;

    public TraceEventCategory Category { get; init; }

    public TraceEventSeverity Severity { get; init; }

    public string Lane { get; init; } = string.Empty;

    public string? CorrelationId { get; init; }
}
