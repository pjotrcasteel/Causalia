namespace Causalia.Verification;

internal sealed class VerificationReportFailureData
{
    public required int TriageRank { get; init; }

    public required string Signature { get; init; }

    public required string Kind { get; init; }

    public required int OccurrenceCount { get; init; }

    public required string Summary { get; init; }

    public required string Trigger { get; init; }

    public required string ScheduleReplayToken { get; init; }

    public string? ReproductionToken { get; init; }
}
