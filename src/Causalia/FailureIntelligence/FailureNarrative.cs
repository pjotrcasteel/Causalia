using Causalia.Consistency;
using Causalia.Exceptions;
using Causalia.Faults;
using Causalia.Linearizability;
using Causalia.ModelBased;
using Causalia.Minimization;

namespace Causalia.FailureIntelligence;

internal static class FailureNarrative
{
    public static string CreateSummary(SimulationFailedException failure, FailureKind kind)
    {
        var exception = failure.InnerException ?? throw new InvalidOperationException("Simulation failure has no inner exception.");
        return kind switch
        {
            FailureKind.InvariantViolation => CreateInvariantSummary(failure.InvariantFailure!),
            FailureKind.InvariantEvaluation => CreateInvariantEvaluationSummary(failure.InvariantFailure!),
            FailureKind.LinearizabilityViolation => CreateLinearizabilitySummary(failure.LinearizabilityFailure!, false),
            FailureKind.LinearizabilityInconclusive => CreateLinearizabilitySummary(failure.LinearizabilityFailure!, true),
            FailureKind.ConsistencyViolation => CreateConsistencySummary(failure.ConsistencyFailure!),
            FailureKind.ModelViolation => CreateModelSummary(failure.ModelFailure!),
            FailureKind.Deadlock => "The simulated system deadlocked before all work completed.",
            FailureKind.StepLimitExceeded => CreateStepLimitSummary((SimulationStepLimitExceededException)exception),
            _ => $"{exception.GetType().Name}: {exception.Message}"
        };
    }

    public static string CreateExplanation(
        FailureTrigger trigger,
        FailureAnalysisConfidence confidence,
        IReadOnlyList<SchedulerChoice> schedulerChoices,
        IReadOnlyList<FaultOccurrence> faults)
    {
        var triggerText = trigger switch
        {
            FailureTrigger.Unknown => "No deterministic reduction was requested, so the triggering dimension is not yet proven.",
            FailureTrigger.Deterministic =>
                "The failure still reproduces with canonical scheduling and with all injected faults suppressed.",
            FailureTrigger.SchedulerOrdering =>
                $"The reduced reproduction depends on scheduler ordering: {DescribeSchedulerChoices(schedulerChoices)}.",
            FailureTrigger.FaultInjection =>
                $"The reduced reproduction depends on fault injection: {DescribeFaults(faults)}.",
            FailureTrigger.SchedulerOrderingAndFaultInjection =>
                $"The reduced reproduction depends on scheduler ordering ({DescribeSchedulerChoices(schedulerChoices)}) and " +
                $"fault injection ({DescribeFaults(faults)}).",
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Unsupported failure trigger.")
        };

        return confidence == FailureAnalysisConfidence.BoundedMinimization
            ? triggerText + " The minimization-attempt budget was exhausted, so additional causes may still be removable."
            : triggerText;
    }

    private static string CreateInvariantSummary(SimulationInvariantException invariant)
    {
        return $"Invariant '{invariant.InvariantName}' ({invariant.Kind}) was violated.";
    }

    private static string CreateInvariantEvaluationSummary(SimulationInvariantException invariant)
    {
        var inner = invariant.InnerException;
        var type = inner?.GetType().Name ?? "Exception";
        return $"Invariant '{invariant.InvariantName}' ({invariant.Kind}) threw {type} while being evaluated.";
    }

    private static string CreateLinearizabilitySummary(SimulationLinearizabilityException failure, bool inconclusive)
    {
        return inconclusive
            ? $"Linearizability history '{failure.HistoryName}' was inconclusive within its configured search bounds."
            : $"Linearizability history '{failure.HistoryName}' has no legal sequential explanation.";
    }

    private static string CreateConsistencySummary(SimulationConsistencyViolationException failure)
    {
        return $"Consistency history '{failure.HistoryName}' violated {failure.Kind}.";
    }

    private static string CreateModelSummary(SimulationModelViolationException failure)
    {
        return $"Model '{failure.ModelName}' diverged on command '{failure.CommandName}' at model step {failure.StepIndex}.";
    }

    private static string CreateStepLimitSummary(SimulationStepLimitExceededException failure)
    {
        return $"The simulation exceeded its configured limit of {failure.MaximumSteps} scheduler steps.";
    }

    private static string DescribeSchedulerChoices(IReadOnlyList<SchedulerChoice> choices)
    {
        if (choices.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ", ",
            choices.Take(3).Select(choice => $"decision {choice.DecisionIndex} -> candidate {choice.SelectedIndex}")) +
            DescribeRemainder(choices.Count);
    }

    private static string DescribeFaults(IReadOnlyList<FaultOccurrence> faults)
    {
        if (faults.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ", ",
            faults.Take(3).Select(fault => $"'{fault.PolicyName}' in '{fault.Scope}' occurrence {fault.Occurrence}")) +
            DescribeRemainder(faults.Count);
    }

    private static string DescribeRemainder(int count)
    {
        return count > 3 ? $", plus {count - 3} more" : string.Empty;
    }
}
