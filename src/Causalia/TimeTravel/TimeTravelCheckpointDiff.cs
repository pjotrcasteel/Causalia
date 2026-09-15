namespace Causalia.TimeTravel;

/// <summary>
/// Contains all watched state changes between two deterministic checkpoints.
/// </summary>
public sealed class TimeTravelCheckpointDiff
{
    internal TimeTravelCheckpointDiff(
        TimeTravelCheckpoint from,
        TimeTravelCheckpoint to,
        IReadOnlyList<TimeTravelStateChange> changes)
    {
        From = from;
        To = to;
        Changes = changes;
    }

    /// <summary>
    /// Gets the earlier checkpoint.
    /// </summary>
    public TimeTravelCheckpoint From { get; }

    /// <summary>
    /// Gets the later checkpoint.
    /// </summary>
    public TimeTravelCheckpoint To { get; }

    /// <summary>
    /// Gets watched state values that differ between the checkpoints.
    /// </summary>
    public IReadOnlyList<TimeTravelStateChange> Changes { get; }

    /// <summary>
    /// Gets whether at least one watched state value changed.
    /// </summary>
    public bool HasChanges => Changes.Count > 0;
}
