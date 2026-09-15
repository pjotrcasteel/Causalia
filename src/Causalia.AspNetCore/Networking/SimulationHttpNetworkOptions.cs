namespace Causalia.AspNetCore.Networking;

/// <summary>
/// Configures deterministic service-to-service HTTP networking inside one simulation.
/// </summary>
public sealed class SimulationHttpNetworkOptions
{
    /// <summary>
    /// Gets or sets whether network lifecycle events are written to the simulation trace.
    /// </summary>
    public bool TraceRequests { get; set; } = true;
}
