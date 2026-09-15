namespace Causalia.Verification;

/// <summary>
/// Describes the platform outcome of one verification step.
/// </summary>
public enum VerificationStepStatus
{
    /// <summary>
    /// The verification step completed without finding a correctness failure.
    /// </summary>
    Passed = 0,

    /// <summary>
    /// The verification step found a deterministic correctness failure.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// The verification step was not executed because the plan stopped after an earlier failure.
    /// </summary>
    Skipped = 2
}
