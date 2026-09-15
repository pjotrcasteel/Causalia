using Causalia.Faults;

namespace Causalia.Grpc;

/// <summary>
/// Configures one directed deterministic gRPC network link.
/// </summary>
public sealed class SimulationGrpcNetworkLink
{
    private readonly FaultInjector<GrpcCallContext, GrpcNetworkFault>? _faults;
    private readonly SimulationContext _context;

    internal SimulationGrpcNetworkLink(SimulationContext context, string source, string destination, GrpcFaultPlan? faults)
    {
        _context = context;
        Source = source;
        Destination = destination;
        _faults = faults is null ? null : context.CreateFaultInjector($"grpc:{source}->{destination}", faults.Plan);
    }

    /// <summary>Gets the source service name.</summary>
    public string Source { get; }

    /// <summary>Gets the destination service name.</summary>
    public string Destination { get; }

    /// <summary>Gets or sets fixed virtual latency for every attempt.</summary>
    public TimeSpan Latency { get; set; }

    /// <summary>Gets whether this directed link is partitioned.</summary>
    public bool IsPartitioned { get; private set; }

    /// <summary>Partitions this directed link.</summary>
    public void Partition()
    {
        IsPartitioned = true;
        _context.TraceEvent($"grpc:link:partitioned:{Source}->{Destination}");
    }

    /// <summary>Heals this directed link.</summary>
    public void Heal()
    {
        IsPartitioned = false;
        _context.TraceEvent($"grpc:link:healed:{Source}->{Destination}");
    }

    internal IReadOnlyList<GrpcNetworkFault> Evaluate(GrpcCallContext context)
    {
        return _faults?.Evaluate(context) ?? [];
    }
}
