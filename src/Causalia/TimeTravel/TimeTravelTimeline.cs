namespace Causalia.TimeTravel;

/// <summary>
/// Contains retained deterministic checkpoints for one simulation execution.
/// </summary>
public sealed class TimeTravelTimeline
{
    internal TimeTravelTimeline(
        IReadOnlyList<TimeTravelCheckpoint> checkpoints,
        long totalCheckpointCount,
        long droppedCheckpointCount)
    {
        Checkpoints = checkpoints;
        TotalCheckpointCount = totalCheckpointCount;
        DroppedCheckpointCount = droppedCheckpointCount;
    }

    /// <summary>
    /// Gets retained checkpoints ordered by capture index.
    /// </summary>
    public IReadOnlyList<TimeTravelCheckpoint> Checkpoints { get; }

    /// <summary>
    /// Gets how many checkpoints were captured before retention was applied.
    /// </summary>
    public long TotalCheckpointCount { get; }

    /// <summary>
    /// Gets how many oldest checkpoints were discarded because of the retention limit.
    /// </summary>
    public long DroppedCheckpointCount { get; }

    /// <summary>
    /// Gets whether one or more older checkpoints were discarded.
    /// </summary>
    public bool Truncated => DroppedCheckpointCount > 0;

    /// <summary>
    /// Gets the first retained checkpoint when one exists.
    /// </summary>
    public TimeTravelCheckpoint? First => Checkpoints.Count == 0 ? null : Checkpoints[0];

    /// <summary>
    /// Gets the last retained checkpoint when one exists.
    /// </summary>
    public TimeTravelCheckpoint? Last => Checkpoints.Count == 0 ? null : Checkpoints[^1];

    /// <summary>
    /// Finds one retained checkpoint by its stable tt1 token.
    /// </summary>
    public TimeTravelCheckpoint? Find(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Checkpoints.FirstOrDefault(value => string.Equals(value.Token, token, StringComparison.Ordinal));
    }

    /// <summary>
    /// Creates a movable debugger cursor positioned at the latest retained checkpoint.
    /// </summary>
    public TimeTravelDebugger CreateDebugger()
    {
        return new TimeTravelDebugger(this);
    }

    /// <summary>
    /// Calculates watched state differences between two retained checkpoints.
    /// </summary>
    public TimeTravelCheckpointDiff Diff(TimeTravelCheckpoint from, TimeTravelCheckpoint to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var before = from.State.ToDictionary(value => value.Name, StringComparer.Ordinal);
        var after = to.State.ToDictionary(value => value.Name, StringComparer.Ordinal);
        var names = before.Keys
            .Concat(after.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);
        var changes = new List<TimeTravelStateChange>();

        foreach (var name in names)
        {
            before.TryGetValue(name, out var previous);
            after.TryGetValue(name, out var current);

            if (previous is null)
            {
                changes.Add(new TimeTravelStateChange(name, TimeTravelStateChangeKind.Added, null, current));
                continue;
            }

            if (current is null)
            {
                changes.Add(new TimeTravelStateChange(name, TimeTravelStateChangeKind.Removed, previous, null));
                continue;
            }

            if (!Equivalent(previous, current))
            {
                changes.Add(new TimeTravelStateChange(name, TimeTravelStateChangeKind.Changed, previous, current));
            }
        }

        return new TimeTravelCheckpointDiff(from, to, changes.AsReadOnly());
    }

    private static bool Equivalent(TimeTravelProbeSnapshot left, TimeTravelProbeSnapshot right)
    {
        return string.Equals(left.Value, right.Value, StringComparison.Ordinal) &&
               string.Equals(left.ErrorType, right.ErrorType, StringComparison.Ordinal) &&
               string.Equals(left.ErrorMessage, right.ErrorMessage, StringComparison.Ordinal);
    }
}
