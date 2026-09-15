namespace Causalia.Load.Profiles;

/// <summary>
/// Runs a fixed number of logical actors, each executing a fixed number of iterations.
/// </summary>
public sealed class FixedConcurrencyLoadProfile : LoadProfile
{
    /// <summary>
    /// Gets or initializes the number of concurrently active logical actors.
    /// </summary>
    public int VirtualUsers { get; init; } = 1;

    /// <summary>
    /// Gets or initializes the number of iterations executed by each virtual user.
    /// </summary>
    public int IterationsPerUser { get; init; } = 1;

    /// <summary>
    /// Gets or initializes deterministic virtual think time between iterations of the same virtual user.
    /// </summary>
    public TimeSpan ThinkTime { get; init; }

    internal override string Name => "fixed-concurrency";

    internal override void Validate()
    {
        if (VirtualUsers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(VirtualUsers), VirtualUsers, "VirtualUsers must be greater than zero.");
        }

        if (IterationsPerUser <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(IterationsPerUser),
                IterationsPerUser,
                "IterationsPerUser must be greater than zero.");
        }

        if (ThinkTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ThinkTime), ThinkTime, "ThinkTime cannot be negative.");
        }
    }
}
