namespace Causalia.Load.Profiles;

/// <summary>
/// Schedules a deterministic burst of iterations at the same virtual instant.
/// </summary>
public sealed class BurstLoadProfile : LoadProfile
{
    /// <summary>
    /// Gets or initializes the number of iterations in the burst.
    /// </summary>
    public int Iterations { get; init; } = 1;

    /// <summary>
    /// Gets or initializes the maximum number of iterations that may be active simultaneously.
    /// </summary>
    public int MaxConcurrentIterations { get; init; } = 1_000;

    internal override string Name => "burst";

    internal override void Validate()
    {
        if (Iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Iterations), Iterations, "Iterations must be greater than zero.");
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
