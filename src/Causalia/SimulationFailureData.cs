using Causalia.Faults;
using Causalia.ProductionReality;
using Causalia.Scheduling;
using Causalia.TimeTravel;
using Causalia.Tracing;

namespace Causalia;

internal sealed class SimulationFailureData
{
    public required ulong Seed { get; init; }

    public required SimulationSchedule Schedule { get; init; }

    public required IReadOnlyList<SimulationTraceEntry> Trace { get; init; }

    public required IReadOnlyList<FaultOccurrence> Faults { get; init; }

    public required TimeTravelTimeline TimeTravel { get; init; }

    public required ProductionRealityEvidence ProductionReality { get; init; }
}
