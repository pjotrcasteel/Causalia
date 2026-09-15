namespace Causalia.Verification;

/// <summary>
/// Indicates that a verification plan completed with one or more failed or skipped steps.
/// </summary>
public sealed class VerificationRunFailedException : Exception
{
    internal VerificationRunFailedException(VerificationRunResult result)
        : base(
            $"Verification plan '{result.PlanName}' did not pass. " +
            $"Failed: {result.FailedCount}; skipped: {result.SkippedCount}; report: {result.Report.Fingerprint}")
    {
        Result = result;
    }

    /// <summary>
    /// Gets the complete verification result that caused this exception.
    /// </summary>
    public VerificationRunResult Result { get; }
}
