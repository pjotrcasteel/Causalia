using Causalia.Scheduling;

namespace Causalia.ModelBased;

/// <summary>
/// Configures strict replay of one model command sequence and optionally one exact scheduler timeline.
/// </summary>
public sealed class ModelBasedReplayOptions
{
    /// <summary>
    /// Gets or initializes the deterministic simulation settings.
    /// </summary>
    public SimulationOptions Simulation { get; init; } = new();

    /// <summary>
    /// Gets or initializes the exact scheduler schedule to replay, or null to run the sequence with seeded deterministic scheduling.
    /// </summary>
    public SimulationSchedule? Schedule { get; init; }
}
