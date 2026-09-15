namespace Causalia.Visualization;

internal sealed class TraceHtmlPayload
{
    public TraceHtmlSummary Summary { get; init; } = new();

    public IReadOnlyList<TraceHtmlLane> Lanes { get; init; } = Array.Empty<TraceHtmlLane>();

    public IReadOnlyList<TraceHtmlEvent> Events { get; init; } = Array.Empty<TraceHtmlEvent>();

    public IReadOnlyList<VisualTimeTravelCheckpoint> TimeTravel { get; init; } = Array.Empty<VisualTimeTravelCheckpoint>();

    public bool Truncated { get; init; }
}
