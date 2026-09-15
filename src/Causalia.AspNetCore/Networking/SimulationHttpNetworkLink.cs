using Causalia.AspNetCore.Networking.Faults;
using Causalia.Faults;

namespace Causalia.AspNetCore.Networking;

/// <summary>
/// Configures one directed deterministic HTTP link between two registered services.
/// </summary>
public sealed class SimulationHttpNetworkLink
{
    private readonly SimulationContext _context;
    private readonly FaultPlan<HttpNetworkRequestContext, HttpNetworkFault> _faultPlan = new();
    private FaultInjector<HttpNetworkRequestContext, HttpNetworkFault>? _faultInjector;
    private int _delayPolicyIndex;
    private int _dropPolicyIndex;
    private int _duplicatePolicyIndex;

    internal SimulationHttpNetworkLink(SimulationContext context, string sourceService, string destinationService)
    {
        _context = context;
        SourceService = sourceService;
        DestinationService = destinationService;
    }

    /// <summary>
    /// Gets the logical source service name.
    /// </summary>
    public string SourceService { get; }

    /// <summary>
    /// Gets the logical destination service name.
    /// </summary>
    public string DestinationService { get; }

    /// <summary>
    /// Gets the constant virtual latency added to every request crossing this link.
    /// </summary>
    public TimeSpan BaseLatency { get; private set; }

    /// <summary>
    /// Gets whether the directed link is currently partitioned.
    /// </summary>
    public bool IsPartitioned { get; private set; }

    /// <summary>
    /// Sets the constant virtual latency for every request crossing this link.
    /// </summary>
    public SimulationHttpNetworkLink Latency(TimeSpan latency)
    {
        if (latency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(latency), latency, "Network latency cannot be negative.");
        }

        BaseLatency = latency;
        _context.TraceEvent($"http-network:link:latency:{SourceService}->{DestinationService}:{latency.Ticks}");
        return this;
    }

    /// <summary>
    /// Adds a deterministic probability of dropping a request.
    /// </summary>
    public SimulationHttpNetworkLink Drop(double probability)
    {
        EnsureFaultPlanMutable();
        _faultPlan.Add(
            new ProbabilityFaultPolicy<HttpNetworkRequestContext, HttpNetworkFault>(
                $"http.drop.{++_dropPolicyIndex}",
                probability,
                static _ => new DropHttpRequestFault()));
        return this;
    }

    /// <summary>
    /// Adds a deterministic probability of creating additional request deliveries.
    /// </summary>
    public SimulationHttpNetworkLink Duplicate(double probability, int additionalCopies = 1)
    {
        if (additionalCopies < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalCopies), additionalCopies, "At least one additional copy is required.");
        }

        EnsureFaultPlanMutable();
        _faultPlan.Add(
            new ProbabilityFaultPolicy<HttpNetworkRequestContext, HttpNetworkFault>(
                $"http.duplicate.{++_duplicatePolicyIndex}",
                probability,
                _ => new DuplicateHttpRequestFault(additionalCopies)));
        return this;
    }

    /// <summary>
    /// Adds a deterministic probability of extra virtual latency for a request.
    /// </summary>
    public SimulationHttpNetworkLink Delay(double probability, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Additional network delay cannot be negative.");
        }

        EnsureFaultPlanMutable();
        _faultPlan.Add(
            new ProbabilityFaultPolicy<HttpNetworkRequestContext, HttpNetworkFault>(
                $"http.delay.{++_delayPolicyIndex}",
                probability,
                _ => new DelayHttpRequestFault(delay)));
        return this;
    }

    /// <summary>
    /// Partitions the directed link until <see cref="Heal"/> is called.
    /// </summary>
    public SimulationHttpNetworkLink Partition()
    {
        IsPartitioned = true;
        _context.TraceEvent($"http-network:link:partitioned:{SourceService}->{DestinationService}");
        return this;
    }

    /// <summary>
    /// Heals a previously partitioned directed link.
    /// </summary>
    public SimulationHttpNetworkLink Heal()
    {
        IsPartitioned = false;
        _context.TraceEvent($"http-network:link:healed:{SourceService}->{DestinationService}");
        return this;
    }

    internal IReadOnlyList<HttpNetworkFault> Evaluate(HttpNetworkRequestContext requestContext)
    {
        _faultInjector ??= _context.CreateFaultInjector(
            $"http-network:{SourceService}->{DestinationService}",
            _faultPlan);
        return _faultInjector.Evaluate(requestContext);
    }

    private void EnsureFaultPlanMutable()
    {
        if (_faultInjector is not null)
        {
            throw new InvalidOperationException("HTTP network fault policies cannot be changed after the link has processed its first request.");
        }
    }
}
