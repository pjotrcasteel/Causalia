namespace Causalia.Minimization;

/// <summary>
/// Forces one non-canonical scheduler choice while replaying a minimized reproduction.
/// </summary>
public sealed record SchedulerChoice
{
    /// <summary>
    /// Initializes a scheduler choice.
    /// </summary>
    public SchedulerChoice(int decisionIndex, int selectedIndex)
    {
        if (decisionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decisionIndex), decisionIndex, "DecisionIndex cannot be negative.");
        }

        if (selectedIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex), selectedIndex, "SelectedIndex must be greater than zero.");
        }

        DecisionIndex = decisionIndex;
        SelectedIndex = selectedIndex;
    }

    /// <summary>
    /// Gets the zero-based branching-decision index.
    /// </summary>
    public int DecisionIndex { get; }

    /// <summary>
    /// Gets the non-canonical runnable candidate selected at this branching decision.
    /// </summary>
    public int SelectedIndex { get; }
}
