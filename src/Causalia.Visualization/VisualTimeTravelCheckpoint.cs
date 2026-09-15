using Causalia.TimeTravel;

namespace Causalia.Visualization;

/// <summary>
/// Contains one deterministic debugger checkpoint prepared for visualization.
/// </summary>
public sealed class VisualTimeTravelCheckpoint
{
    internal VisualTimeTravelCheckpoint()
    {
    }

    /// <summary>
    /// Gets the monotonic checkpoint index.
    /// </summary>
    public long Index { get; internal init; }

    /// <summary>
    /// Gets the stable tt1 checkpoint token.
    /// </summary>
    public string Token { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the completed scheduler-step count at this checkpoint.
    /// </summary>
    public int Step { get; internal init; }

    /// <summary>
    /// Gets the virtual timestamp at this checkpoint.
    /// </summary>
    public DateTimeOffset Timestamp { get; internal init; }

    /// <summary>
    /// Gets why this checkpoint was captured.
    /// </summary>
    public TimeTravelCheckpointKind Kind { get; internal init; }

    /// <summary>
    /// Gets the optional user-defined label.
    /// </summary>
    public string? Label { get; internal init; }

    /// <summary>
    /// Gets how many trace events existed at this checkpoint.
    /// </summary>
    public int TraceCount { get; internal init; }

    /// <summary>
    /// Gets watched state captured at this checkpoint.
    /// </summary>
    public IReadOnlyList<VisualTimeTravelProbe> State { get; internal init; } = Array.Empty<VisualTimeTravelProbe>();
}
