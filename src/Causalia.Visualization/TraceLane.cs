namespace Causalia.Visualization;

/// <summary>
/// Describes one logical lane in a trace document.
/// </summary>
public sealed class TraceLane
{
    internal TraceLane()
    {
    }

    /// <summary>
    /// Gets the stable lane name.
    /// </summary>
    public string Name { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the number of events assigned to this lane.
    /// </summary>
    public int EventCount { get; internal init; }
}
