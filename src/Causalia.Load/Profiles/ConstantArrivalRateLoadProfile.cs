namespace Causalia.Load.Profiles;

/// <summary>
/// Starts iterations at a constant open-model arrival rate independent of iteration completion time.
/// </summary>
public sealed class ConstantArrivalRateLoadProfile : LoadProfile
{
    /// <summary>
    /// Gets or initializes the number of iterations scheduled during each time unit.
    /// </summary>
    public int Rate { get; init; } = 1;

    /// <summary>
    /// Gets or initializes the virtual time unit to which <see cref="Rate"/> applies.
    /// </summary>
    public TimeSpan TimeUnit { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or initializes the total virtual duration for which arrivals are scheduled.
    /// </summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or initializes the maximum number of simultaneously active iterations.
    /// </summary>
    public int MaxConcurrentIterations { get; init; } = 1_000;

    internal override string Name => "constant-arrival-rate";

    internal override void Validate()
    {
        if (Rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Rate), Rate, "Rate must be greater than zero.");
        }

        if (TimeUnit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeUnit), TimeUnit, "TimeUnit must be greater than zero.");
        }

        if (Duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "Duration must be greater than zero.");
        }

        if (MaxConcurrentIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrentIterations),
                MaxConcurrentIterations,
                "MaxConcurrentIterations must be greater than zero.");
        }
    }
}
