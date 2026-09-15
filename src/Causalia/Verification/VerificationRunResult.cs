using Causalia.FailureIntelligence;

namespace Causalia.Verification;

/// <summary>
/// Contains the complete typed outcome of one verification-plan execution.
/// </summary>
public sealed class VerificationRunResult
{
    internal VerificationRunResult(string planName, IReadOnlyList<VerificationStepResult> steps)
    {
        PlanName = planName;
        Steps = steps;
        var analyses = steps
            .Where(value => value.FailureAnalysis is not null)
            .Select(value => value.FailureAnalysis!)
            .ToList()
            .AsReadOnly();
        FailureIntelligence = FailureAnalyzer.CreateReport(analyses);
        Report = VerificationReport.Create(planName, steps, FailureIntelligence);
    }

    /// <summary>
    /// Gets the stable verification-plan name.
    /// </summary>
    public string PlanName { get; }

    /// <summary>
    /// Gets step outcomes in deterministic plan order.
    /// </summary>
    public IReadOnlyList<VerificationStepResult> Steps { get; }

    /// <summary>
    /// Gets unified semantic failure clustering across every failed plan step.
    /// </summary>
    public FailureIntelligenceReport FailureIntelligence { get; }

    /// <summary>
    /// Gets the portable deterministic CI report for this verification outcome.
    /// </summary>
    public VerificationReport Report { get; }

    /// <summary>
    /// Gets whether every verification step passed.
    /// </summary>
    public bool Passed => Steps.All(value => value.Status == VerificationStepStatus.Passed);

    /// <summary>
    /// Gets the number of passing steps.
    /// </summary>
    public int PassedCount => Steps.Count(value => value.Status == VerificationStepStatus.Passed);

    /// <summary>
    /// Gets the number of failed steps.
    /// </summary>
    public int FailedCount => Steps.Count(value => value.Status == VerificationStepStatus.Failed);

    /// <summary>
    /// Gets the number of skipped steps.
    /// </summary>
    public int SkippedCount => Steps.Count(value => value.Status == VerificationStepStatus.Skipped);

    /// <summary>
    /// Throws a platform exception carrying this result when one or more steps did not pass.
    /// </summary>
    public void EnsurePassed()
    {
        if (!Passed)
        {
            throw new VerificationRunFailedException(this);
        }
    }
}
