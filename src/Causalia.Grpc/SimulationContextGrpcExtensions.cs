namespace Causalia.Grpc;

/// <summary>
/// Provides gRPC simulation factories for a simulation context.
/// </summary>
public static class SimulationContextGrpcExtensions
{
    /// <summary>
    /// Creates a deterministic gRPC network with optional default fault policies for every directed link.
    /// </summary>
    public static SimulationGrpcNetwork CreateGrpcNetwork(this SimulationContext context, GrpcFaultPlan? faults = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimulationGrpcNetwork(context, faults);
    }
}
