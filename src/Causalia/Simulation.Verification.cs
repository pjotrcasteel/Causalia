using Causalia.Verification;

namespace Causalia;

public static partial class Simulation
{
    /// <summary>
    /// Executes an ordered verification plan and returns one unified deterministic platform result.
    /// </summary>
    public static async Task<VerificationRunResult> VerifyAsync(
        VerificationPlan plan,
        VerificationRunOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var runOptions = options ?? new VerificationRunOptions();
        ValidateVerificationOptions(runOptions);
        var results = new List<VerificationStepResult>(plan.StepCount);
        var stop = false;

        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stop)
            {
                results.Add(CreateSkippedVerificationStep(step));
                continue;
            }

            var result = await step.ExecuteAsync(runOptions, cancellationToken);
            results.Add(result);
            stop = runOptions.StopOnFirstFailure && result.Status == VerificationStepStatus.Failed;
        }

        return new VerificationRunResult(plan.Name, results.AsReadOnly());
    }

    private static VerificationStepResult CreateSkippedVerificationStep(VerificationStepDefinition step)
    {
        return new VerificationStepResult(
            new VerificationStepResultData
            {
                Name = step.Name,
                Kind = step.Kind,
                Status = VerificationStepStatus.Skipped,
                Seed = step.Seed,
                SkipReason = "Skipped because StopOnFirstFailure was enabled and an earlier step failed."
            });
    }

    private static void ValidateVerificationOptions(VerificationRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.FailureAnalysis);

        if (options.TraceContextEntries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.TraceContextEntries,
                "Trace context entry count cannot be negative.");
        }
    }
}
