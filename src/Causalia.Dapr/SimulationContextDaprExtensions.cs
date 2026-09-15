using Causalia.AspNetCore.Networking;

namespace Causalia.Dapr;

/// <summary>
/// Provides Dapr simulation factories for a simulation context.
/// </summary>
public static class SimulationContextDaprExtensions
{
    /// <summary>
    /// Creates a deterministic Dapr environment with optional service-invocation networking.
    /// </summary>
    public static SimulationDaprEnvironment CreateDaprEnvironment(
        this SimulationContext context,
        SimulationHttpNetwork? network = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimulationDaprEnvironment(context, network);
    }
}
