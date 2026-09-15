using Causalia.Exceptions;
using Causalia.Faults;
using Causalia.Minimization;
using Causalia.Scheduling;
using Causalia.Tracing;

namespace Causalia.FailureIntelligence;

/// <summary>
/// Contains semantic classification, deterministic trigger attribution and compact evidence for one failure.
/// </summary>
public sealed class FailureAnalysis
{
    internal FailureAnalysis(FailureAnalysisData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OriginalFailure = data.OriginalFailure;
        RepresentativeFailure = data.RepresentativeFailure;
        Signature = data.Signature;
        Kind = data.Signature.Kind;
        Trigger = data.Trigger;
        Confidence = data.Confidence;
        Summary = data.Summary;
        Explanation = data.Explanation;
        Minimization = data.Minimization;
        TraceContext = data.TraceContext;
    }

    /// <summary>
    /// Gets the original captured deterministic failure.
    /// </summary>
    public SimulationFailedException OriginalFailure { get; }

    /// <summary>
    /// Gets the smallest reproduced failure when minimization ran, otherwise the original failure.
    /// </summary>
    public SimulationFailedException RepresentativeFailure { get; }

    /// <summary>
    /// Gets the seed- and schedule-independent semantic failure signature.
    /// </summary>
    public FailureSignature Signature { get; }

    /// <summary>
    /// Gets the semantic failure classification.
    /// </summary>
    public FailureKind Kind { get; }

    /// <summary>
    /// Gets the controlled simulation dimensions that remain necessary to reproduce this failure.
    /// </summary>
    public FailureTrigger Trigger { get; }

    /// <summary>
    /// Gets how strongly this analysis is supported by deterministic reduction work.
    /// </summary>
    public FailureAnalysisConfidence Confidence { get; }

    /// <summary>
    /// Gets a compact human-readable description of what failed.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets a deterministic explanation of the evidence supporting the trigger attribution.
    /// </summary>
    public string Explanation { get; }

    /// <summary>
    /// Gets the bounded minimization result when causal reduction was requested.
    /// </summary>
    public MinimizationResult? Minimization { get; }

    /// <summary>
    /// Gets the compact reproduction when causal reduction was requested.
    /// </summary>
    public SimulationReproduction? Reproduction => Minimization?.Reproduction;

    /// <summary>
    /// Gets the non-canonical scheduler choices that remain in the compact reproduction.
    /// </summary>
    public IReadOnlyList<SchedulerChoice> EssentialSchedulerChoices =>
        Reproduction?.SchedulerChoices ?? Array.Empty<SchedulerChoice>();

    /// <summary>
    /// Gets the fault occurrences that remain in the compact reproduction.
    /// </summary>
    public IReadOnlyList<FaultOccurrence> EssentialFaults => Reproduction?.Faults ?? Array.Empty<FaultOccurrence>();

    /// <summary>
    /// Gets final trace entries from the representative reproduction for human diagnosis.
    /// </summary>
    public IReadOnlyList<SimulationTraceEntry> TraceContext { get; }

    /// <summary>
    /// Gets the exact scheduler replay token for the original failure.
    /// </summary>
    public string ScheduleReplayToken => OriginalFailure.Schedule.ReplayToken;

    /// <summary>
    /// Gets the minimized reproduction token when causal reduction was requested.
    /// </summary>
    public string? ReproductionToken => Reproduction?.ReplayToken;
}
