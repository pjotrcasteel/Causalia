namespace Causalia.Coverage;

/// <summary>
/// Captures the distinct coverage points observed during one deterministic simulation run.
/// </summary>
public sealed class CoverageSnapshot
{
    internal CoverageSnapshot(IReadOnlyList<string> points)
    {
        Points = points;
    }

    /// <summary>
    /// Gets the stable ordered set of coverage points observed during the run.
    /// </summary>
    public IReadOnlyList<string> Points { get; }

    /// <summary>
    /// Gets the number of distinct coverage points observed during the run.
    /// </summary>
    public int Count => Points.Count;

    /// <summary>
    /// Returns whether this snapshot contains the supplied coverage point.
    /// </summary>
    public bool Contains(string point)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(point);
        return Points.Contains(point, StringComparer.Ordinal);
    }
}
