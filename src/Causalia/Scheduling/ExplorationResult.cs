namespace Causalia.Scheduling;

/// <summary>
/// Describes the outcome of a bounded schedule exploration that completed without a scenario failure.
/// </summary>
public sealed class ExplorationResult
{
    internal ExplorationResult(ExplorationResultData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Strategy = data.Strategy;
        SchedulesExplored = data.SchedulesExplored;
        ExhaustedWithinBounds = data.ExhaustedWithinBounds;
        DepthLimitReached = data.DepthLimitReached;
        CoveragePointsDiscovered = data.CoveragePointsDiscovered;
        SchedulesWithNewCoverage = data.SchedulesWithNewCoverage;
        EquivalentSchedulesPruned = data.EquivalentSchedulesPruned;
        PreemptionBoundPruned = data.PreemptionBoundPruned;
        MaximumPreemptionsObserved = data.MaximumPreemptionsObserved;
    }

    /// <summary>
    /// Gets the strategy used to prioritize explored schedules.
    /// </summary>
    public ExplorationStrategy Strategy { get; }

    /// <summary>
    /// Gets the number of distinct schedules that were executed.
    /// </summary>
    public int SchedulesExplored { get; }

    /// <summary>
    /// Gets whether every schedule reachable within the configured decision-depth bound was explored.
    /// </summary>
    public bool ExhaustedWithinBounds { get; }

    /// <summary>
    /// Gets whether at least one explored run contained branching decisions beyond the configured depth bound.
    /// </summary>
    public bool DepthLimitReached { get; }

    /// <summary>
    /// Gets the number of distinct coverage points discovered across all explored schedules.
    /// </summary>
    public int CoveragePointsDiscovered { get; }

    /// <summary>
    /// Gets the number of schedules that contributed at least one previously unseen coverage point.
    /// </summary>
    public int SchedulesWithNewCoverage { get; }

    /// <summary>
    /// Gets the number of DPOR alternatives skipped because declared operation dependencies proved them equivalent.
    /// </summary>
    public int EquivalentSchedulesPruned { get; }

    /// <summary>
    /// Gets the number of DPOR alternatives skipped because they exceeded the configured preemption bound.
    /// </summary>
    public int PreemptionBoundPruned { get; }

    /// <summary>
    /// Gets the greatest number of scheduler preemptions observed in an executed DPOR schedule.
    /// </summary>
    public int MaximumPreemptionsObserved { get; }
}
