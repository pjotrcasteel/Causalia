namespace Causalia.Load.Profiles;

/// <summary>
/// Starts iterations according to a deterministic piecewise-linear open-model arrival-rate curve.
/// </summary>
public sealed class RampingArrivalRateLoadProfile : LoadProfile
{
    /// <summary>
    /// Gets or initializes the iteration rate at the beginning of the first stage.
    /// </summary>
    public int StartRate { get; init; }

    /// <summary>
    /// Gets or initializes the virtual time unit to which all stage rates apply.
    /// </summary>
    public TimeSpan TimeUnit { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or initializes the ordered piecewise-linear arrival-rate stages.
    /// </summary>
    public IReadOnlyList<ArrivalRateStage> Stages { get; init; } = Array.Empty<ArrivalRateStage>();

    /// <summary>
    /// Gets or initializes the maximum number of simultaneously active iterations.
    /// </summary>
    public int MaxConcurrentIterations { get; init; } = 1_000;

    internal override string Name => "ramping-arrival-rate";

    internal override void Validate()
    {
        if (StartRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StartRate), StartRate, "StartRate cannot be negative.");
        }

        if (TimeUnit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeUnit), TimeUnit, "TimeUnit must be greater than zero.");
        }

        if (Stages.Count == 0)
        {
            throw new ArgumentException("At least one arrival-rate stage is required.", nameof(Stages));
        }

        if (MaxConcurrentIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrentIterations),
                MaxConcurrentIterations,
                "MaxConcurrentIterations must be greater than zero.");
        }

        foreach (var stage in Stages)
        {
            ArgumentNullException.ThrowIfNull(stage);

            if (stage.Duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(Stages), stage.Duration, "Stage durations must be greater than zero.");
            }

            if (stage.TargetRate < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Stages), stage.TargetRate, "Stage target rates cannot be negative.");
            }
        }
    }
}
