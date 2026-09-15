namespace Causalia.TimeTravel;

internal sealed class TimeTravelCheckpointData
{
    public required long Index { get; init; }

    public required int Step { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required TimeTravelCheckpointKind Kind { get; init; }

    public string? Label { get; init; }

    public required int TraceCount { get; init; }

    public required IReadOnlyList<TimeTravelProbeSnapshot> State { get; init; }
}
