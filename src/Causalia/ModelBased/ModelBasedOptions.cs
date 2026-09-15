namespace Causalia.ModelBased;

/// <summary>
/// Configures bounded state-machine exploration for model-based testing.
/// </summary>
public sealed class ModelBasedOptions
{
    /// <summary>
    /// Gets or initializes the deterministic simulation settings reused for every model sequence.
    /// </summary>
    public SimulationOptions Simulation { get; init; } = new();

    /// <summary>
    /// Gets or initializes the maximum number of non-empty command sequences executed.
    /// </summary>
    public int MaxSequences { get; init; } = 1_000;

    /// <summary>
    /// Gets or initializes the maximum number of commands in one explored sequence.
    /// </summary>
    public int MaxCommandDepth { get; init; } = 20;

    /// <summary>
    /// Gets or initializes optional scheduler exploration performed independently for every command sequence.
    /// </summary>
    public ModelBasedScheduleExplorationOptions? ScheduleExploration { get; init; }
}
