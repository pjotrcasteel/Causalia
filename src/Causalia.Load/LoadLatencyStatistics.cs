namespace Causalia.Load;

/// <summary>
/// Contains deterministic virtual-duration statistics for completed load iterations.
/// </summary>
public sealed class LoadLatencyStatistics
{
    internal LoadLatencyStatistics(
        TimeSpan minimum,
        TimeSpan mean,
        TimeSpan median,
        TimeSpan percentile95,
        TimeSpan percentile99,
        TimeSpan maximum)
    {
        Minimum = minimum;
        Mean = mean;
        Median = median;
        Percentile95 = percentile95;
        Percentile99 = percentile99;
        Maximum = maximum;
    }

    /// <summary>
    /// Gets the minimum observed virtual iteration duration.
    /// </summary>
    public TimeSpan Minimum { get; }

    /// <summary>
    /// Gets the arithmetic mean virtual iteration duration.
    /// </summary>
    public TimeSpan Mean { get; }

    /// <summary>
    /// Gets the 50th percentile virtual iteration duration.
    /// </summary>
    public TimeSpan Median { get; }

    /// <summary>
    /// Gets the 95th percentile virtual iteration duration.
    /// </summary>
    public TimeSpan Percentile95 { get; }

    /// <summary>
    /// Gets the 99th percentile virtual iteration duration.
    /// </summary>
    public TimeSpan Percentile99 { get; }

    /// <summary>
    /// Gets the maximum observed virtual iteration duration.
    /// </summary>
    public TimeSpan Maximum { get; }
}
