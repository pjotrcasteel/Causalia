namespace Causalia.Verification;

internal sealed class VerificationStepDefinition
{
    public required string Name { get; init; }

    public required VerificationStepKind Kind { get; init; }

    public required ulong Seed { get; init; }

    public required Func<VerificationRunOptions, CancellationToken, Task<VerificationStepResult>> ExecuteAsync { get; init; }
}
