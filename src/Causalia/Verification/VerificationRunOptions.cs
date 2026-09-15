using Causalia.FailureIntelligence;

namespace Causalia.Verification;

/// <summary>
/// Configures execution and reporting for a verification plan.
/// </summary>
public sealed class VerificationRunOptions
{
    /// <summary>
    /// Gets or sets whether remaining verification steps are skipped after the first correctness failure.
    /// </summary>
    public bool StopOnFirstFailure { get; init; }

    /// <summary>
    /// Gets or sets whether scenario and schedule-exploration failures are minimized before they are added to the unified failure report.
    /// </summary>
    public bool MinimizeFailures { get; init; }

    /// <summary>
    /// Gets or sets deterministic failure-analysis options used when <see cref="MinimizeFailures"/> is enabled.
    /// </summary>
    public FailureAnalysisOptions FailureAnalysis { get; init; } = new();

    /// <summary>
    /// Gets or sets the number of final trace entries retained when failures are described without minimization.
    /// </summary>
    public int TraceContextEntries { get; init; } = 25;
}
