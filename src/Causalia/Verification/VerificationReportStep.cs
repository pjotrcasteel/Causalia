namespace Causalia.Verification;

/// <summary>
/// Contains portable deterministic report data for one verification step.
/// </summary>
public sealed class VerificationReportStep
{
    internal VerificationReportStep(VerificationReportStepData data)
    {
        Name = data.Name;
        Kind = data.Kind;
        Status = data.Status;
        Seed = data.Seed;
        Summary = data.Summary;
        FailureSignature = data.FailureSignature;
        ScheduleReplayToken = data.ScheduleReplayToken;
        TimeTravelCheckpointCount = data.TimeTravelCheckpointCount;
        ProductionRealityFingerprints = data.ProductionRealityFingerprints;
    }

    /// <summary>
    /// Gets the verification-plan step name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the verification engine used by this step.
    /// </summary>
    public VerificationStepKind Kind { get; }

    /// <summary>
    /// Gets whether the step passed, failed, or was skipped.
    /// </summary>
    public VerificationStepStatus Status { get; }

    /// <summary>
    /// Gets the deterministic seed configured for the step.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets a compact deterministic step summary.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the stable fi2 failure signature when this step failed.
    /// </summary>
    public string? FailureSignature { get; }

    /// <summary>
    /// Gets the exact scheduler replay token when this step produced a seeded simulation failure or success.
    /// </summary>
    public string? ScheduleReplayToken { get; }

    /// <summary>
    /// Gets the number of retained debugger checkpoints attached to this step's evidence.
    /// </summary>
    public int TimeTravelCheckpointCount { get; }

    /// <summary>
    /// Gets production-reality dataset fingerprints consumed by this step.
    /// </summary>
    public IReadOnlyList<string> ProductionRealityFingerprints { get; }
}
