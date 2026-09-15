namespace Causalia.Verification;

internal sealed class VerificationReportStepData
{
    public required string Name { get; init; }

    public required VerificationStepKind Kind { get; init; }

    public required VerificationStepStatus Status { get; init; }

    public required ulong Seed { get; init; }

    public required string Summary { get; init; }

    public string? FailureSignature { get; init; }

    public string? ScheduleReplayToken { get; init; }

    public required int TimeTravelCheckpointCount { get; init; }

    public required IReadOnlyList<string> ProductionRealityFingerprints { get; init; }
}
