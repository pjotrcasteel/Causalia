using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.ModelBased;
using Causalia.Scheduling;

namespace Causalia.Verification;

internal sealed class VerificationStepResultData
{
    public required string Name { get; init; }

    public required VerificationStepKind Kind { get; init; }

    public required VerificationStepStatus Status { get; init; }

    public required ulong Seed { get; init; }

    public SimulationResult? Simulation { get; init; }

    public ExplorationResult? Exploration { get; init; }

    public ModelBasedExplorationResult? ModelBased { get; init; }

    public SimulationFailedException? Failure { get; init; }

    public FailureAnalysis? FailureAnalysis { get; init; }

    public string? SkipReason { get; init; }
}
