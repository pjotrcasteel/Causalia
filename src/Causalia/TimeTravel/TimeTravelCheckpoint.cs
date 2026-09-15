namespace Causalia.TimeTravel;

/// <summary>
/// Represents one immutable point on a deterministic simulation timeline.
/// </summary>
public sealed class TimeTravelCheckpoint
{
    internal TimeTravelCheckpoint(TimeTravelCheckpointData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Index = data.Index;
        Step = data.Step;
        Timestamp = data.Timestamp;
        Kind = data.Kind;
        Label = data.Label;
        TraceCount = data.TraceCount;
        State = data.State;
        Token = $"tt1:{data.Index}";
    }

    /// <summary>
    /// Gets the monotonic checkpoint index for this execution.
    /// </summary>
    public long Index { get; }

    /// <summary>
    /// Gets the stable checkpoint token for navigation and diagnostics.
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the number of scheduler steps completed at this point.
    /// </summary>
    public int Step { get; }

    /// <summary>
    /// Gets the virtual timestamp at this point.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets why the checkpoint was captured.
    /// </summary>
    public TimeTravelCheckpointKind Kind { get; }

    /// <summary>
    /// Gets the optional user-defined checkpoint label.
    /// </summary>
    public string? Label { get; }

    /// <summary>
    /// Gets how many trace entries existed when this checkpoint was captured.
    /// </summary>
    public int TraceCount { get; }

    /// <summary>
    /// Gets all registered state-probe snapshots ordered by probe name.
    /// </summary>
    public IReadOnlyList<TimeTravelProbeSnapshot> State { get; }

    /// <summary>
    /// Finds one captured state probe by its stable name.
    /// </summary>
    public TimeTravelProbeSnapshot? FindState(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return State.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal));
    }
}
