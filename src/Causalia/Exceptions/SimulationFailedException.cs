using Causalia.Consistency;
using Causalia.FailureIntelligence;
using Causalia.Faults;
using Causalia.Scheduling;
using Causalia.Linearizability;
using Causalia.ModelBased;
using Causalia.ProductionReality;
using Causalia.Tracing;
using Causalia.TimeTravel;

namespace Causalia.Exceptions;

/// <summary>
/// Wraps a scenario failure together with the deterministic state required for replay.
/// </summary>
public sealed class SimulationFailedException : Exception
{
    internal SimulationFailedException(SimulationFailureData data, Exception innerException)
        : base(
            $"Simulation failed with seed {data.Seed}. Replay token: {data.Schedule.ReplayToken}",
            innerException)
    {
        ArgumentNullException.ThrowIfNull(data);
        Seed = data.Seed;
        Schedule = data.Schedule;
        Trace = data.Trace;
        Faults = data.Faults;
        TimeTravel = data.TimeTravel;
        ProductionReality = data.ProductionReality;
        InvariantFailure = innerException as SimulationInvariantException;
        LinearizabilityFailure = innerException as SimulationLinearizabilityException;
        ConsistencyFailure = innerException as SimulationConsistencyViolationException;
        ModelFailure = innerException as SimulationModelViolationException;
        Signature = FailureAnalyzer.GetSignature(this);
    }

    /// <summary>
    /// Gets the seed that produced the failure.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the exact branching schedule that produced the failure.
    /// </summary>
    public SimulationSchedule Schedule { get; }

    /// <summary>
    /// Gets the trace captured before the failure.
    /// </summary>
    public IReadOnlyList<SimulationTraceEntry> Trace { get; }

    /// <summary>
    /// Gets the deterministic fault occurrences that took effect before the failure.
    /// </summary>
    public IReadOnlyList<FaultOccurrence> Faults { get; }

    /// <summary>
    /// Gets deterministic debugger checkpoints retained before the failure.
    /// </summary>
    public TimeTravelTimeline TimeTravel { get; }

    /// <summary>
    /// Gets production evidence consumed before this deterministic failure occurred.
    /// </summary>
    public ProductionRealityEvidence ProductionReality { get; }

    /// <summary>
    /// Gets the invariant failure when this simulation failed because an invariant was violated or could not be evaluated.
    /// </summary>
    public SimulationInvariantException? InvariantFailure { get; }

    /// <summary>
    /// Gets the linearizability failure when this simulation failed because a concurrent history was invalid or inconclusive.
    /// </summary>
    public SimulationLinearizabilityException? LinearizabilityFailure { get; }

    /// <summary>
    /// Gets the consistency failure when this simulation failed because a distributed-consistency guarantee was violated.
    /// </summary>
    public SimulationConsistencyViolationException? ConsistencyFailure { get; }

    /// <summary>
    /// Gets the model-based verification failure when the system-under-test observation did not match the executable model.
    /// </summary>
    public SimulationModelViolationException? ModelFailure { get; }

    /// <summary>
    /// Gets the stable semantic failure signature used for grouping equivalent failures across seeds and schedules.
    /// </summary>
    public FailureSignature Signature { get; }

    /// <summary>
    /// Gets the semantic failure classification.
    /// </summary>
    public FailureKind Kind => Signature.Kind;
}
