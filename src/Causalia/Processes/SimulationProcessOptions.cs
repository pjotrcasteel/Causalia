namespace Causalia.Processes;

/// <summary>
/// Configures a deterministic simulated process.
/// </summary>
public sealed class SimulationProcessOptions
{
    /// <summary>
    /// Gets or sets the stable logical process name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets whether process lifecycle events are written to the simulation trace.
    /// </summary>
    public bool TraceLifecycle { get; init; } = true;
}
