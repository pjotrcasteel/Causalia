namespace Causalia.Scheduling;

/// <summary>
/// Describes one scheduler choice made at a branching point where more than one continuation was runnable.
/// </summary>
public sealed class ScheduleDecision
{
    internal ScheduleDecision(int decisionIndex, int step, int selectedIndex, int candidateCount, long workItemId)
    {
        DecisionIndex = decisionIndex;
        Step = step;
        SelectedIndex = selectedIndex;
        CandidateCount = candidateCount;
        WorkItemId = workItemId;
    }

    /// <summary>
    /// Gets the zero-based branching decision index within the schedule.
    /// </summary>
    public int DecisionIndex { get; }

    /// <summary>
    /// Gets the one-based scheduler step on which the decision was made.
    /// </summary>
    public int Step { get; }

    /// <summary>
    /// Gets the selected zero-based index within the runnable candidate set.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// Gets the number of runnable candidates available when the decision was made.
    /// </summary>
    public int CandidateCount { get; }

    /// <summary>
    /// Gets the deterministic work-item identifier selected by this decision.
    /// </summary>
    public long WorkItemId { get; }
}
