namespace Causalia.Visualization;

/// <summary>
/// Contains a normalized, presentation-ready deterministic simulation trace.
/// </summary>
public sealed class SimulationTraceDocument
{
    internal SimulationTraceDocument()
    {
    }

    /// <summary>
    /// Gets execution-level summary data.
    /// </summary>
    public TraceSummary Summary { get; internal init; } = new();

    /// <summary>
    /// Gets the logical lanes present in the trace.
    /// </summary>
    public IReadOnlyList<TraceLane> Lanes { get; internal init; } = Array.Empty<TraceLane>();

    /// <summary>
    /// Gets normalized events in original trace order.
    /// </summary>
    public IReadOnlyList<VisualTraceEvent> Events { get; internal init; } = Array.Empty<VisualTraceEvent>();

    /// <summary>
    /// Gets deterministic time-travel checkpoints retained for this execution.
    /// </summary>
    public IReadOnlyList<VisualTimeTravelCheckpoint> TimeTravel { get; internal init; } = Array.Empty<VisualTimeTravelCheckpoint>();
}
