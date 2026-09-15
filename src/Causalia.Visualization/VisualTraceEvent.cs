namespace Causalia.Visualization;

/// <summary>
/// Represents one normalized event shown by a Causalia trace visualizer.
/// </summary>
public sealed class VisualTraceEvent
{
    internal VisualTraceEvent()
    {
    }

    /// <summary>
    /// Gets the zero-based event index in the original trace.
    /// </summary>
    public int Index { get; internal init; }

    /// <summary>
    /// Gets the deterministic scheduler step.
    /// </summary>
    public int Step { get; internal init; }

    /// <summary>
    /// Gets the virtual timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; internal init; }

    /// <summary>
    /// Gets virtual elapsed time relative to the first trace event.
    /// </summary>
    public TimeSpan Elapsed { get; internal init; }

    /// <summary>
    /// Gets the original trace message.
    /// </summary>
    public string Message { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the normalized event category.
    /// </summary>
    public TraceEventCategory Category { get; internal init; }

    /// <summary>
    /// Gets the visual severity.
    /// </summary>
    public TraceEventSeverity Severity { get; internal init; }

    /// <summary>
    /// Gets the logical lane used to group related activity.
    /// </summary>
    public string Lane { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets a correlation identifier when the event belongs to a causal chain.
    /// </summary>
    public string? CorrelationId { get; internal init; }
}
