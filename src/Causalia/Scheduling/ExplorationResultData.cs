namespace Causalia.Scheduling;

internal sealed class ExplorationResultData
{
    public required ExplorationStrategy Strategy { get; init; }

    public required int SchedulesExplored { get; init; }

    public required bool ExhaustedWithinBounds { get; init; }

    public required bool DepthLimitReached { get; init; }

    public required int CoveragePointsDiscovered { get; init; }

    public required int SchedulesWithNewCoverage { get; init; }

    public required int EquivalentSchedulesPruned { get; init; }

    public required int PreemptionBoundPruned { get; init; }

    public required int MaximumPreemptionsObserved { get; init; }
}
