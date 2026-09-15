namespace Causalia.Tracing;

/// <summary>
/// Represents one event in a deterministic simulation trace.
/// </summary>
public sealed class SimulationTraceEntry
{
    internal SimulationTraceEntry(int step, DateTimeOffset timestamp, string message)
    {
        Step = step;
        Timestamp = timestamp;
        Message = message;
    }

    /// <summary>
    /// Gets the scheduler step at which the event was captured.
    /// </summary>
    public int Step { get; }

    /// <summary>
    /// Gets the virtual timestamp at which the event was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the event description.
    /// </summary>
    public string Message { get; }
}
