namespace Causalia.Visualization;

/// <summary>
/// Indicates the visual severity assigned to a trace event.
/// </summary>
public enum TraceEventSeverity
{
    /// <summary>
    /// Normal informational activity.
    /// </summary>
    Information,

    /// <summary>
    /// A successful terminal or recovery event.
    /// </summary>
    Success,

    /// <summary>
    /// An unusual but not necessarily failing event.
    /// </summary>
    Warning,

    /// <summary>
    /// A failure, violation or interrupted operation.
    /// </summary>
    Error
}
