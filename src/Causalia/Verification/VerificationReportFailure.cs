namespace Causalia.Verification;

/// <summary>
/// Contains portable triage data for one semantic failure cluster in a verification report.
/// </summary>
public sealed class VerificationReportFailure
{
    internal VerificationReportFailure(VerificationReportFailureData data)
    {
        TriageRank = data.TriageRank;
        Signature = data.Signature;
        Kind = data.Kind;
        OccurrenceCount = data.OccurrenceCount;
        Summary = data.Summary;
        Trigger = data.Trigger;
        ScheduleReplayToken = data.ScheduleReplayToken;
        ReproductionToken = data.ReproductionToken;
    }

    /// <summary>
    /// Gets the one-based deterministic engineering triage rank.
    /// </summary>
    public int TriageRank { get; }

    /// <summary>
    /// Gets the stable fi2 semantic failure signature.
    /// </summary>
    public string Signature { get; }

    /// <summary>
    /// Gets the semantic failure-kind name.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the number of occurrences represented by this cluster.
    /// </summary>
    public int OccurrenceCount { get; }

    /// <summary>
    /// Gets a compact human-readable description of the representative failure.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the deterministic trigger-attribution name for the representative occurrence.
    /// </summary>
    public string Trigger { get; }

    /// <summary>
    /// Gets the exact scheduler replay token for the representative occurrence.
    /// </summary>
    public string ScheduleReplayToken { get; }

    /// <summary>
    /// Gets the minimized m1 reproduction token when causal reduction was enabled.
    /// </summary>
    public string? ReproductionToken { get; }
}
