using Causalia.Consistency;
using Causalia.Coverage;
using Causalia.Faults;
using Causalia.Exploration;
using Causalia.Invariants;
using Causalia.Linearizability;
using Causalia.Messaging;
using Causalia.Nodes;
using Causalia.Processes;
using Causalia.ProductionReality;
using Causalia.Randomness;
using Causalia.Runtime;
using Causalia.Tracing;
using Causalia.TimeTravel;

namespace Causalia;

/// <summary>
/// Exposes deterministic runtime services to a simulation scenario.
/// </summary>
public sealed class SimulationContext
{
    private readonly Dictionary<string, SimulationNode> _nodes = new(StringComparer.Ordinal);
    private readonly DeterministicScheduler _scheduler;
    private long _nextFaultInjectorId;

    internal SimulationContext(DeterministicScheduler scheduler, CancellationToken cancellationToken)
    {
        _scheduler = scheduler;
        CancellationToken = cancellationToken;
        Linearizability = new SimulationLinearizability(this);
        Exploration = new SimulationExploration(scheduler);
    }

    /// <summary>
    /// Gets the seed for the current simulation run.
    /// </summary>
    public ulong Seed => _scheduler.Seed;

    /// <summary>
    /// Gets the virtual time provider for deterministic delays and timers.
    /// </summary>
    public TimeProvider TimeProvider => _scheduler.TimeProvider;

    /// <summary>
    /// Gets deterministic user-facing randomness isolated from scheduler and fault random streams.
    /// </summary>
    public SimulationRandom Random => _scheduler.Random;

    /// <summary>
    /// Gets the cancellation token for the current simulation run.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the trace captured so far by the simulation.
    /// </summary>
    public IReadOnlyList<SimulationTraceEntry> Trace => _scheduler.Trace;

    /// <summary>
    /// Gets the coverage recorder used by coverage-guided exploration.
    /// </summary>
    public SimulationCoverage Coverage => _scheduler.Coverage;

    /// <summary>
    /// Gets the invariant registry for deterministic safety and liveness requirements.
    /// </summary>
    public SimulationInvariants Invariants => _scheduler.Invariants;

    /// <summary>
    /// Gets the distributed-consistency verification API for logical read/write histories.
    /// </summary>
    public SimulationConsistency Consistency => _scheduler.Consistency;

    /// <summary>
    /// Gets the advanced exploration API used to declare logical operation dependencies for partial-order reduction.
    /// </summary>
    public SimulationExploration Exploration { get; }

    /// <summary>
    /// Gets deterministic checkpoint and state-probe services for time-travel debugging.
    /// </summary>
    public SimulationTimeTravel TimeTravel => _scheduler.TimeTravel;

    /// <summary>
    /// Gets production-derived timing, outcome sampling and incident replay services.
    /// </summary>
    public SimulationProductionReality ProductionReality => _scheduler.ProductionReality;

    /// <summary>
    /// Gets the deterministic linearizability history factory for concurrent operation checking.
    /// </summary>
    public SimulationLinearizability Linearizability { get; }

    /// <summary>
    /// Creates a running simulated node with a stable logical name.
    /// </summary>
    public SimulationNode CreateNode(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_nodes.ContainsKey(name))
        {
            throw new InvalidOperationException($"A simulated node named '{name}' already exists.");
        }

        var node = new SimulationNode(this, name);
        _nodes.Add(name, node);
        return node;
    }

    /// <summary>
    /// Starts a restartable simulated process whose generation factory is rerun after every restart.
    /// </summary>
    public Task<SimulationProcess<TGeneration>> StartProcessAsync<TGeneration>(
        SimulationProcessOptions options,
        Func<SimulationProcessGenerationContext, CancellationToken, Task<TGeneration>> generationFactory,
        CancellationToken cancellationToken)
        where TGeneration : class, ISimulationProcessGeneration
    {
        return SimulationProcess<TGeneration>.StartAsync(this, options, generationFactory, cancellationToken);
    }

    /// <summary>
    /// Creates a deterministic in-memory message bus bound to this simulation.
    /// </summary>
    public SimulationMessageBus CreateMessageBus(MessageBusOptions? options = null)
    {
        return new SimulationMessageBus(this, options ?? new MessageBusOptions());
    }

    /// <summary>
    /// Creates an isolated deterministic fault injector for any simulation-domain event and effect types.
    /// </summary>
    public FaultInjector<TContext, TEffect> CreateFaultInjector<TContext, TEffect>(
        string scope,
        FaultPlan<TContext, TEffect> plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(plan);
        var injectorId = checked(++_nextFaultInjectorId);
        var scopeSeed = FaultInjector<TContext, TEffect>.StableHash(scope);
        var injectorSeed = FaultInjector<TContext, TEffect>.MixForStream((ulong)injectorId);
        var streamSeed = Seed ^ scopeSeed ^ injectorSeed;
        return new FaultInjector<TContext, TEffect>(plan, streamSeed, scope, injectorId, _scheduler.Faults);
    }

    internal void TrackOperation(Task operation)
    {
        _scheduler.TrackOperation(operation);
    }

    internal void Dispose()
    {
        foreach (var node in _nodes.Values)
        {
            node.Dispose();
        }

        _nodes.Clear();
    }

    /// <summary>
    /// Adds a user-defined event to the deterministic simulation trace.
    /// </summary>
    public void TraceEvent(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _scheduler.RecordTrace(message);
    }

    /// <summary>
    /// Starts two asynchronous operations together and completes when both operations complete.
    /// </summary>
    public async Task ConcurrentAsync(
        Func<CancellationToken, Task> first,
        Func<CancellationToken, Task> second,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        cancellationToken.ThrowIfCancellationRequested();

        var firstTask = first(cancellationToken);
        var secondTask = second(cancellationToken);
        await Task.WhenAll(firstTask, secondTask);
    }
}
