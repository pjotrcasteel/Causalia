namespace Causalia.Load.Profiles;

/// <summary>
/// Defines one linear target-rate stage in a deterministic ramping arrival-rate profile.
/// </summary>
public sealed class ArrivalRateStage
{
    /// <summary>
    /// Gets or initializes the virtual duration over which the current rate ramps to <see cref="TargetRate"/>.
    /// </summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or initializes the target iteration rate reached at the end of the stage.
    /// </summary>
    public int TargetRate { get; init; }
}
