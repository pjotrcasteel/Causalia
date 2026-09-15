namespace Causalia.ProductionReality;

/// <summary>
/// Summarizes production evidence for one logical operation.
/// </summary>
public sealed class ProductionRealityOperationProfile
{
    private readonly IReadOnlyList<ProductionRealityObservation> _observations;

    internal ProductionRealityOperationProfile(string operation, IReadOnlyList<ProductionRealityObservation> observations)
    {
        Operation = operation;
        _observations = observations;
        SampleCount = observations.Count;
        FailureRate = Ratio(observations, ProductionRealityOutcome.Failure);
        TimeoutRate = Ratio(observations, ProductionRealityOutcome.Timeout);
        CancelledRate = Ratio(observations, ProductionRealityOutcome.Cancelled);
        MinimumDuration = observations.Min(value => value.Duration);
        MaximumDuration = observations.Max(value => value.Duration);
        Percentile50 = Percentile(observations, 0.50);
        Percentile95 = Percentile(observations, 0.95);
        Percentile99 = Percentile(observations, 0.99);
    }

    /// <summary>
    /// Gets the logical operation name.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the number of production observations represented by this profile.
    /// </summary>
    public int SampleCount { get; }

    /// <summary>
    /// Gets the fraction of observations classified as failures.
    /// </summary>
    public double FailureRate { get; }

    /// <summary>
    /// Gets the fraction of observations classified as timeouts.
    /// </summary>
    public double TimeoutRate { get; }

    /// <summary>
    /// Gets the fraction of observations classified as cancelled.
    /// </summary>
    public double CancelledRate { get; }

    /// <summary>
    /// Gets the minimum observed production duration.
    /// </summary>
    public TimeSpan MinimumDuration { get; }

    /// <summary>
    /// Gets the maximum observed production duration.
    /// </summary>
    public TimeSpan MaximumDuration { get; }

    /// <summary>
    /// Gets the nearest-rank production p50 duration.
    /// </summary>
    public TimeSpan Percentile50 { get; }

    /// <summary>
    /// Gets the nearest-rank production p95 duration.
    /// </summary>
    public TimeSpan Percentile95 { get; }

    /// <summary>
    /// Gets the nearest-rank production p99 duration.
    /// </summary>
    public TimeSpan Percentile99 { get; }

    internal IReadOnlyList<ProductionRealityObservation> Observations => _observations;

    private static double Ratio(IReadOnlyList<ProductionRealityObservation> observations, ProductionRealityOutcome outcome)
    {
        return observations.Count(value => value.Outcome == outcome) / (double)observations.Count;
    }

    private static TimeSpan Percentile(IReadOnlyList<ProductionRealityObservation> observations, double percentile)
    {
        var ordered = observations.Select(value => value.Duration).OrderBy(value => value).ToArray();
        var index = Math.Max(0, (int)Math.Ceiling(percentile * ordered.Length) - 1);
        return ordered[index];
    }
}
