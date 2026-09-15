namespace Causalia.Load.Thresholds;

/// <summary>
/// Describes one deterministic load threshold that was exceeded.
/// </summary>
public sealed class LoadThresholdViolation
{
    internal LoadThresholdViolation(string threshold, string observed, string limit)
    {
        Threshold = threshold;
        Observed = observed;
        Limit = limit;
    }

    /// <summary>
    /// Gets the stable threshold name.
    /// </summary>
    public string Threshold { get; }

    /// <summary>
    /// Gets the observed value formatted using invariant culture.
    /// </summary>
    public string Observed { get; }

    /// <summary>
    /// Gets the configured limit formatted using invariant culture.
    /// </summary>
    public string Limit { get; }
}
