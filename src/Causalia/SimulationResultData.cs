using Causalia.Consistency;
using Causalia.Coverage;
using Causalia.Faults;
using Causalia.Invariants;
using Causalia.Runtime;
using Causalia.ProductionReality;
using Causalia.Scheduling;
using Causalia.Tracing;
using Causalia.TimeTravel;

namespace Causalia;

internal sealed class SimulationResultData
{
    public required ulong Seed { get; init; }

    public required int Steps { get; init; }

    public required TimeSpan VirtualElapsed { get; init; }

    public required SimulationSchedule Schedule { get; init; }

    public required IReadOnlyList<SimulationTraceEntry> Trace { get; init; }

    public required IReadOnlyList<InvariantOutcome> Invariants { get; init; }

    public required IReadOnlyList<ConsistencyOutcome> Consistency { get; init; }

    public required IReadOnlyList<FaultOccurrence> Faults { get; init; }

    public required CoverageSnapshot Coverage { get; init; }

    public required ExplorationExecutionMetadata ExplorationMetadata { get; init; }

    public required TimeTravelTimeline TimeTravel { get; init; }

    public required ProductionRealityEvidence ProductionReality { get; init; }
}
