namespace Causalia.Load.Thresholds;

/// <summary>
/// Defines optional deterministic acceptance thresholds for a completed load run.
/// </summary>
public sealed class LoadThresholds
{
    /// <summary>
    /// Gets or initializes the maximum allowed failed-iteration fraction between zero and one.
    /// </summary>
    public double? MaximumFailureRate { get; init; }

    /// <summary>
    /// Gets or initializes the maximum allowed dropped-iteration fraction between zero and one.
    /// </summary>
    public double? MaximumDroppedRate { get; init; }

    /// <summary>
    /// Gets or initializes the maximum allowed 95th percentile virtual iteration duration.
    /// </summary>
    public TimeSpan? MaximumPercentile95 { get; init; }

    /// <summary>
    /// Gets or initializes the maximum allowed 99th percentile virtual iteration duration.
    /// </summary>
    public TimeSpan? MaximumPercentile99 { get; init; }

    /// <summary>
    /// Gets or initializes the maximum allowed peak concurrency.
    /// </summary>
    public int? MaximumPeakConcurrency { get; init; }

    internal void Validate()
    {
        ValidateRate(MaximumFailureRate, nameof(MaximumFailureRate));
        ValidateRate(MaximumDroppedRate, nameof(MaximumDroppedRate));

        if (MaximumPercentile95 < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPercentile95),
                MaximumPercentile95,
                "MaximumPercentile95 cannot be negative.");
        }

        if (MaximumPercentile99 < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPercentile99),
                MaximumPercentile99,
                "MaximumPercentile99 cannot be negative.");
        }

        if (MaximumPeakConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPeakConcurrency),
                MaximumPeakConcurrency,
                "MaximumPeakConcurrency must be greater than zero when specified.");
        }
    }

    private static void ValidateRate(double? value, string parameterName)
    {
        if (value is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Rate thresholds must be between zero and one.");
        }
    }
}
