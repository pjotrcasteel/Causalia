using Causalia.Consistency;
using Causalia.Coverage;
using Causalia.Faults;
using Causalia.Invariants;
using Causalia.Scheduling;
using Causalia.Runtime;
using Causalia.ProductionReality;
using Causalia.Tracing;
using Causalia.TimeTravel;

namespace Causalia;

/// <summary>
/// Describes the deterministic outcome of a completed simulation run.
/// </summary>
public sealed class SimulationResult
{
    internal SimulationResult(SimulationResultData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Seed = data.Seed;
        Steps = data.Steps;
        VirtualElapsed = data.VirtualElapsed;
        Schedule = data.Schedule;
        Trace = data.Trace;
        Invariants = data.Invariants;
        Consistency = data.Consistency;
        Faults = data.Faults;
        Coverage = data.Coverage;
        ExplorationMetadata = data.ExplorationMetadata;
        TimeTravel = data.TimeTravel;
        ProductionReality = data.ProductionReality;
    }

    /// <summary>
    /// Gets the seed that produced this run.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the number of deterministic scheduler steps that were executed.
    /// </summary>
    public int Steps { get; }

    /// <summary>
    /// Gets the amount of virtual time that elapsed during the run.
    /// </summary>
    public TimeSpan VirtualElapsed { get; }

    /// <summary>
    /// Gets the exact branching schedule captured during this run.
    /// </summary>
    public SimulationSchedule Schedule { get; }

    /// <summary>
    /// Gets the ordered trace captured during the run.
    /// </summary>
    public IReadOnlyList<SimulationTraceEntry> Trace { get; }

    /// <summary>
    /// Gets the invariants that completed successfully during this run.
    /// </summary>
    public IReadOnlyList<InvariantOutcome> Invariants { get; }

    /// <summary>
    /// Gets the distributed-consistency histories that completed successfully during this run.
    /// </summary>
    public IReadOnlyList<ConsistencyOutcome> Consistency { get; }

    /// <summary>
    /// Gets the deterministic fault occurrences that took effect during this run.
    /// </summary>
    public IReadOnlyList<FaultOccurrence> Faults { get; }

    /// <summary>
    /// Gets the stable coverage points observed during this run.
    /// </summary>
    public CoverageSnapshot Coverage { get; }

    /// <summary>
    /// Gets deterministic debugger checkpoints retained for this execution.
    /// </summary>
    public TimeTravelTimeline TimeTravel { get; }

    /// <summary>
    /// Gets production evidence actually consumed by this deterministic execution.
    /// </summary>
    public ProductionRealityEvidence ProductionReality { get; }

    internal ExplorationExecutionMetadata ExplorationMetadata { get; }
}
