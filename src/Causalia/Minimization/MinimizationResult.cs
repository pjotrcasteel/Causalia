using Causalia.Exceptions;

namespace Causalia.Minimization;

/// <summary>
/// Describes the reduced reproduction found by bounded delta debugging within the configured minimization budget.
/// </summary>
public sealed class MinimizationResult
{
    internal MinimizationResult(
        SimulationFailedException originalFailure,
        SimulationFailedException minimizedFailure,
        SimulationReproduction reproduction,
        int attempts,
        bool exhaustedBudget)
    {
        OriginalFailure = originalFailure;
        MinimizedFailure = minimizedFailure;
        Reproduction = reproduction;
        Attempts = attempts;
        ExhaustedBudget = exhaustedBudget;
        OriginalScheduleDecisionCount = originalFailure.Schedule.Decisions.Count;
        OriginalNonCanonicalSchedulerChoiceCount = originalFailure.Schedule.Decisions.Count(decision => decision.SelectedIndex != 0);
        OriginalFaultCount = originalFailure.Faults.Count;
    }

    /// <summary>
    /// Gets the original deterministic failure supplied to the minimizer.
    /// </summary>
    public SimulationFailedException OriginalFailure { get; }

    /// <summary>
    /// Gets the failure produced by the minimized reproduction.
    /// </summary>
    public SimulationFailedException MinimizedFailure { get; }

    /// <summary>
    /// Gets the compact reproduction found by the minimizer.
    /// </summary>
    public SimulationReproduction Reproduction { get; }

    /// <summary>
    /// Gets the number of candidate reproductions executed while minimizing.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Gets whether minimization stopped because <see cref="MinimizationOptions.MaxAttempts"/> was reached.
    /// </summary>
    public bool ExhaustedBudget { get; }

    /// <summary>
    /// Gets the number of branching decisions captured by the original concrete schedule.
    /// </summary>
    public int OriginalScheduleDecisionCount { get; }

    /// <summary>
    /// Gets the number of non-canonical scheduler choices in the original failure.
    /// </summary>
    public int OriginalNonCanonicalSchedulerChoiceCount { get; }

    /// <summary>
    /// Gets the number of non-canonical scheduler choices that remain essential.
    /// </summary>
    public int EssentialSchedulerChoiceCount => Reproduction.SchedulerChoices.Count;

    /// <summary>
    /// Gets the number of triggered faults in the original failure.
    /// </summary>
    public int OriginalFaultCount { get; }

    /// <summary>
    /// Gets the number of fault occurrences that remain essential.
    /// </summary>
    public int EssentialFaultCount => Reproduction.Faults.Count;
}
