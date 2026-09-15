namespace Causalia.TimeTravel;

/// <summary>
/// Describes one watched state difference between two deterministic checkpoints.
/// </summary>
public sealed class TimeTravelStateChange
{
    internal TimeTravelStateChange(
        string name,
        TimeTravelStateChangeKind kind,
        TimeTravelProbeSnapshot? before,
        TimeTravelProbeSnapshot? after)
    {
        Name = name;
        Kind = kind;
        Before = before;
        After = after;
    }

    /// <summary>
    /// Gets the stable state-probe name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets how the watched state changed.
    /// </summary>
    public TimeTravelStateChangeKind Kind { get; }

    /// <summary>
    /// Gets the earlier state observation when one existed.
    /// </summary>
    public TimeTravelProbeSnapshot? Before { get; }

    /// <summary>
    /// Gets the later state observation when one existed.
    /// </summary>
    public TimeTravelProbeSnapshot? After { get; }
}
