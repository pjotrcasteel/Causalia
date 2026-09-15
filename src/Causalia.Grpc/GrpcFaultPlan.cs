using Causalia.Faults;

namespace Causalia.Grpc;

/// <summary>
/// Configures deterministic probabilistic faults for a gRPC network link.
/// </summary>
public sealed class GrpcFaultPlan
{
    internal FaultPlan<GrpcCallContext, GrpcNetworkFault> Plan { get; } = new();

    /// <summary>
    /// Adds additional virtual latency with the specified deterministic probability.
    /// </summary>
    public GrpcFaultPlan Delay(double probability, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        Plan.Add(new ProbabilityFaultPolicy<GrpcCallContext, GrpcNetworkFault>(
            "grpc.delay",
            probability,
            _ => new DelayGrpcFault(delay)));
        return this;
    }

    /// <summary>
    /// Fails an attempt with UNAVAILABLE using the specified deterministic probability.
    /// </summary>
    public GrpcFaultPlan Unavailable(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<GrpcCallContext, GrpcNetworkFault>(
            "grpc.unavailable",
            probability,
            _ => new UnavailableGrpcFault()));
        return this;
    }
}
