using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.ModelBased;
using Causalia.ProductionReality;
using Causalia.Scheduling;
using Causalia.TimeTravel;

namespace Causalia.Verification;

/// <summary>
/// Contains the typed outcome and deterministic evidence for one verification-plan step.
/// </summary>
public sealed class VerificationStepResult
{
    internal VerificationStepResult(VerificationStepResultData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Name = data.Name;
        Kind = data.Kind;
        Status = data.Status;
        Seed = data.Seed;
        Simulation = data.Simulation;
        Exploration = data.Exploration;
        ModelBased = data.ModelBased;
        Failure = data.Failure;
        FailureAnalysis = data.FailureAnalysis;
        SkipReason = data.SkipReason;
    }

    /// <summary>
    /// Gets the stable unique step name from the plan.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the verification engine used by this step.
    /// </summary>
    public VerificationStepKind Kind { get; }

    /// <summary>
    /// Gets whether this step passed, failed, or was skipped.
    /// </summary>
    public VerificationStepStatus Status { get; }

    /// <summary>
    /// Gets the deterministic simulation seed configured for this step.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the successful seeded simulation result when this was a simulation step.
    /// </summary>
    public SimulationResult? Simulation { get; }

    /// <summary>
    /// Gets the successful schedule-exploration result when this was an exploration step.
    /// </summary>
    public ExplorationResult? Exploration { get; }

    /// <summary>
    /// Gets the successful model-based exploration result when this was a model step.
    /// </summary>
    public ModelBasedExplorationResult? ModelBased { get; }

    /// <summary>
    /// Gets the deterministic correctness failure when this step failed.
    /// </summary>
    public SimulationFailedException? Failure { get; }

    /// <summary>
    /// Gets semantic failure intelligence for a failed step.
    /// </summary>
    public FailureAnalysis? FailureAnalysis { get; }

    /// <summary>
    /// Gets why this step was skipped, when applicable.
    /// </summary>
    public string? SkipReason { get; }

    /// <summary>
    /// Gets deterministic time-travel evidence for seeded simulation steps and failed exploration/model steps.
    /// </summary>
    public TimeTravelTimeline? TimeTravel => Simulation?.TimeTravel ?? Failure?.TimeTravel;

    /// <summary>
    /// Gets production-reality evidence consumed by seeded simulation steps and failed exploration/model steps.
    /// </summary>
    public ProductionRealityEvidence? ProductionReality => Simulation?.ProductionReality ?? Failure?.ProductionReality;
}
