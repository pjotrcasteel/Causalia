using Causalia.Exceptions;
using Causalia.Faults;
using Causalia.Minimization;
using Causalia.Scheduling;
using Causalia.Tracing;

namespace Causalia.FailureIntelligence;

/// <summary>
/// Creates semantic signatures, human-readable diagnostics and deterministic failure clusters.
/// </summary>
public static class FailureAnalyzer
{
    /// <summary>
    /// Creates a stable semantic signature for one deterministic failure.
    /// </summary>
    public static FailureSignature GetSignature(SimulationFailedException failure)
    {
        return FailureSignatureFactory.Create(failure);
    }

    /// <summary>
    /// Classifies one captured failure without rerunning the scenario.
    /// Trigger attribution remains unknown until deterministic minimization is requested.
    /// </summary>
    public static FailureAnalysis Describe(SimulationFailedException failure, int traceContextEntries = 25)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ValidateTraceContextEntries(traceContextEntries);
        var signature = GetSignature(failure);
        return new FailureAnalysis(
            new FailureAnalysisData
            {
                OriginalFailure = failure,
                RepresentativeFailure = failure,
                Signature = signature,
                Trigger = FailureTrigger.Unknown,
                Confidence = FailureAnalysisConfidence.Descriptive,
                Summary = FailureNarrative.CreateSummary(failure, signature.Kind),
                Explanation = FailureNarrative.CreateExplanation(
                    FailureTrigger.Unknown,
                    FailureAnalysisConfidence.Descriptive,
                    Array.Empty<SchedulerChoice>(),
                    Array.Empty<FaultOccurrence>()),
                Minimization = null,
                TraceContext = CreateTraceContext(failure.Trace, traceContextEntries)
            });
    }

    /// <summary>
    /// Groups analyzed occurrences by semantic signature and orders the groups for engineering triage.
    /// </summary>
    public static FailureIntelligenceReport CreateReport(IReadOnlyList<FailureAnalysis> analyses)
    {
        ArgumentNullException.ThrowIfNull(analyses);

        if (analyses.Any(value => value is null))
        {
            throw new ArgumentException("Failure analyses cannot contain null values.", nameof(analyses));
        }

        var candidates = analyses
            .GroupBy(value => value.Signature.Token, StringComparer.Ordinal)
            .Select(CreateClusterCandidate)
            .OrderByDescending(value => value.Occurrences.Count)
            .ThenByDescending(value => value.Representative.Confidence)
            .ThenBy(value => CountEssentialElements(value.Representative))
            .ThenBy(value => value.Signature.Token, StringComparer.Ordinal)
            .ToList();
        var clusters = candidates
            .Select(
                (candidate, index) => new FailureCluster(
                    index + 1,
                    candidate.Signature,
                    candidate.Occurrences,
                    candidate.Representative))
            .ToList()
            .AsReadOnly();
        return new FailureIntelligenceReport(clusters, analyses.Count);
    }

    internal static FailureAnalysis Create(
        SimulationFailedException originalFailure,
        MinimizationResult minimization,
        int traceContextEntries)
    {
        ArgumentNullException.ThrowIfNull(originalFailure);
        ArgumentNullException.ThrowIfNull(minimization);
        ValidateTraceContextEntries(traceContextEntries);
        var signature = GetSignature(originalFailure);
        var trigger = ClassifyTrigger(minimization.Reproduction);
        var confidence = minimization.ExhaustedBudget
            ? FailureAnalysisConfidence.BoundedMinimization
            : FailureAnalysisConfidence.Minimized;
        var representativeFailure = minimization.MinimizedFailure;
        var schedulerChoices = minimization.Reproduction.SchedulerChoices;
        var faults = minimization.Reproduction.Faults;
        return new FailureAnalysis(
            new FailureAnalysisData
            {
                OriginalFailure = originalFailure,
                RepresentativeFailure = representativeFailure,
                Signature = signature,
                Trigger = trigger,
                Confidence = confidence,
                Summary = FailureNarrative.CreateSummary(representativeFailure, signature.Kind),
                Explanation = FailureNarrative.CreateExplanation(trigger, confidence, schedulerChoices, faults),
                Minimization = minimization,
                TraceContext = CreateTraceContext(representativeFailure.Trace, traceContextEntries)
            });
    }

    private static FailureTrigger ClassifyTrigger(SimulationReproduction reproduction)
    {
        var hasSchedulerChoices = reproduction.SchedulerChoices.Count > 0;
        var hasFaults = reproduction.Faults.Count > 0;

        return (hasSchedulerChoices, hasFaults) switch
        {
            (false, false) => FailureTrigger.Deterministic,
            (true, false) => FailureTrigger.SchedulerOrdering,
            (false, true) => FailureTrigger.FaultInjection,
            (true, true) => FailureTrigger.SchedulerOrderingAndFaultInjection
        };
    }

    private static IReadOnlyList<SimulationTraceEntry> CreateTraceContext(
        IReadOnlyList<SimulationTraceEntry> trace,
        int traceContextEntries)
    {
        if (traceContextEntries == 0 || trace.Count == 0)
        {
            return Array.Empty<SimulationTraceEntry>();
        }

        return trace.Skip(Math.Max(0, trace.Count - traceContextEntries)).ToList().AsReadOnly();
    }

    private static FailureClusterCandidate CreateClusterCandidate(IGrouping<string, FailureAnalysis> group)
    {
        var occurrences = group.ToList().AsReadOnly();
        var representative = occurrences
            .OrderByDescending(value => value.Confidence)
            .ThenBy(CountEssentialElements)
            .ThenBy(value => value.OriginalFailure.Seed)
            .ThenBy(value => value.ScheduleReplayToken, StringComparer.Ordinal)
            .First();
        return new FailureClusterCandidate(representative.Signature, occurrences, representative);
    }

    private static int CountEssentialElements(FailureAnalysis analysis)
    {
        return analysis.EssentialSchedulerChoices.Count + analysis.EssentialFaults.Count;
    }

    private static void ValidateTraceContextEntries(int traceContextEntries)
    {
        if (traceContextEntries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(traceContextEntries),
                traceContextEntries,
                "Trace context entry count cannot be negative.");
        }
    }

    private sealed record FailureClusterCandidate(
        FailureSignature Signature,
        IReadOnlyList<FailureAnalysis> Occurrences,
        FailureAnalysis Representative);
}
