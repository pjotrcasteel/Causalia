using Causalia.Exploration;
using Causalia.Scheduling;

namespace Causalia.Runtime;

internal static class DporScheduleAnalyzer
{
    public static DporAnalysisResult Analyze(SimulationResult result, ExplorationOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        var metadata = result.ExplorationMetadata;

        if (metadata is null)
        {
            return Empty();
        }

        var alternatives = new List<IReadOnlyList<int>>();
        var equivalentPruned = 0;
        var preemptionPruned = 0;
        var boundedCount = Math.Min(metadata.Decisions.Count, options.MaxDecisionDepth);

        for (var decisionIndex = boundedCount - 1; decisionIndex >= 0; decisionIndex--)
        {
            var decision = metadata.Decisions[decisionIndex];
            var selectedOperationId = decision.CandidateOperationIds[decision.SelectedIndex];

            for (var candidateIndex = decision.CandidateOperationIds.Count - 1; candidateIndex >= 0; candidateIndex--)
            {
                if (candidateIndex == decision.SelectedIndex)
                {
                    continue;
                }

                var candidateOperationId = decision.CandidateOperationIds[candidateIndex];

                if (AreIndependent(metadata, selectedOperationId, candidateOperationId))
                {
                    equivalentPruned++;
                    continue;
                }

                var prefix = CreatePrefix(result.Schedule, decisionIndex, candidateIndex);
                var preemptions = CountPreemptions(metadata, prefix);

                if (options.MaxPreemptions is not null && preemptions > options.MaxPreemptions.Value)
                {
                    preemptionPruned++;
                    continue;
                }

                alternatives.Add(prefix);
            }
        }

        var observedPrefix = result.Schedule.Decisions.Select(decision => decision.SelectedIndex).ToArray();
        return new DporAnalysisResult
        {
            AlternativePrefixes = alternatives.AsReadOnly(),
            EquivalentSchedulesPruned = equivalentPruned,
            PreemptionBoundPruned = preemptionPruned,
            PreemptionsObserved = CountPreemptions(metadata, observedPrefix)
        };
    }

    private static bool AreIndependent(
        ExplorationExecutionMetadata metadata,
        long? firstOperationId,
        long? secondOperationId)
    {
        if (firstOperationId is null || secondOperationId is null || firstOperationId == secondOperationId)
        {
            return false;
        }

        if (!metadata.Operations.TryGetValue(firstOperationId.Value, out var first) ||
            !metadata.Operations.TryGetValue(secondOperationId.Value, out var second))
        {
            return false;
        }

        foreach (var firstAccess in first.Accesses)
        {
            foreach (var secondAccess in second.Accesses)
            {
                if (!string.Equals(firstAccess.Resource, secondAccess.Resource, StringComparison.Ordinal))
                {
                    continue;
                }

                if (Conflicts(firstAccess.Kind, secondAccess.Kind))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool Conflicts(ExplorationAccessKind first, ExplorationAccessKind second)
    {
        return first != ExplorationAccessKind.Read || second != ExplorationAccessKind.Read;
    }

    private static IReadOnlyList<int> CreatePrefix(SimulationSchedule schedule, int decisionIndex, int selectedIndex)
    {
        var prefix = new int[decisionIndex + 1];

        for (var index = 0; index < decisionIndex; index++)
        {
            prefix[index] = schedule.Decisions[index].SelectedIndex;
        }

        prefix[decisionIndex] = selectedIndex;
        return Array.AsReadOnly(prefix);
    }

    private static int CountPreemptions(ExplorationExecutionMetadata metadata, IReadOnlyList<int> prefix)
    {
        long? previousOperationId = null;
        var preemptions = 0;
        var count = Math.Min(prefix.Count, metadata.Decisions.Count);

        for (var index = 0; index < count; index++)
        {
            var decision = metadata.Decisions[index];
            var selectedIndex = prefix[index];

            if (selectedIndex < 0 || selectedIndex >= decision.CandidateOperationIds.Count)
            {
                break;
            }

            var selectedOperationId = decision.CandidateOperationIds[selectedIndex];

            if (previousOperationId is not null && selectedOperationId != previousOperationId &&
                decision.CandidateOperationIds.Contains(previousOperationId))
            {
                preemptions++;
            }

            previousOperationId = selectedOperationId;
        }

        return preemptions;
    }

    private static DporAnalysisResult Empty()
    {
        return new DporAnalysisResult
        {
            AlternativePrefixes = Array.Empty<IReadOnlyList<int>>(),
            EquivalentSchedulesPruned = 0,
            PreemptionBoundPruned = 0,
            PreemptionsObserved = 0
        };
    }
}
