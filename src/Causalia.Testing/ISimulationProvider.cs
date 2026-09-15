using Causalia.Consistency;
using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.Minimization;
using Causalia.Linearizability;
using Causalia.ModelBased;
using Causalia.ProductionReality;
using Causalia.Scheduling;
using Causalia.TimeTravel;
using Causalia.Verification;

namespace Causalia.Testing;

/// <summary>
/// Provides scenario-scoped access to Causalia without coupling a test framework to the core runtime.
/// </summary>
public interface ISimulationProvider
{
    /// <summary>
    /// Gets the options used for the next seeded simulation run or schedule replay.
    /// </summary>
    SimulationOptions Options { get; }

    /// <summary>
    /// Gets the most recent successful simulation result for this provider instance.
    /// </summary>
    SimulationResult? LastResult { get; }

    /// <summary>
    /// Gets the most recent deterministic simulation failure for this provider instance.
    /// </summary>
    SimulationFailedException? LastFailure { get; }

    /// <summary>
    /// Gets semantic failure intelligence for the most recent deterministic failure when one is available.
    /// </summary>
    FailureAnalysis? LastFailureAnalysis => LastFailure is null ? null : FailureAnalyzer.Describe(LastFailure);

    /// <summary>
    /// Gets deterministic debugger checkpoints from the most recent successful or failed execution.
    /// </summary>
    TimeTravelTimeline? LastTimeTravel => LastResult?.TimeTravel ?? LastFailure?.TimeTravel;

    /// <summary>
    /// Gets production evidence consumed by the most recent successful or failed execution.
    /// </summary>
    ProductionRealityEvidence? LastProductionReality => LastResult?.ProductionReality ?? LastFailure?.ProductionReality;

    /// <summary>
    /// Gets the most recent invariant failure observed by a seeded run, replay, exploration, or minimized reproduction.
    /// </summary>
    SimulationInvariantException? LastInvariantFailure { get; }

    /// <summary>
    /// Gets the most recent linearizability failure observed by a run, replay, exploration, or minimized reproduction.
    /// </summary>
    SimulationLinearizabilityException? LastLinearizabilityFailure { get; }

    /// <summary>
    /// Gets the most recent distributed-consistency failure observed by a run, replay, exploration, or minimized reproduction.
    /// </summary>
    SimulationConsistencyViolationException? LastConsistencyFailure => LastFailure?.ConsistencyFailure;

    /// <summary>
    /// Gets the most recent model-based verification failure observed by a run, replay, exploration, or model check.
    /// </summary>
    SimulationModelViolationException? LastModelFailure => LastFailure?.ModelFailure;

    /// <summary>
    /// Gets the most recent successful schedule-exploration result for this provider instance.
    /// </summary>
    ExplorationResult? LastExplorationResult { get; }

    /// <summary>
    /// Gets the most recent failure discovered by systematic schedule exploration.
    /// </summary>
    SimulationExplorationFailedException? LastExplorationFailure { get; }

    /// <summary>
    /// Gets the most recent failure-minimization result for this provider instance.
    /// </summary>
    MinimizationResult? LastMinimizationResult { get; }

    /// <summary>
    /// Gets the most recent unified verification-platform result for this provider instance.
    /// </summary>
    VerificationRunResult? LastVerificationResult => null;

    /// <summary>
    /// Replaces the options used by subsequent seeded simulation runs and replays.
    /// </summary>
    void Configure(SimulationOptions options);

    /// <summary>
    /// Executes a seeded deterministic simulation through this provider.
    /// </summary>
    Task<SimulationResult> RunAsync(Func<SimulationContext, Task> scenario, CancellationToken cancellationToken);

    /// <summary>
    /// Replays an exact branching schedule through this provider.
    /// </summary>
    Task<SimulationResult> ReplayAsync(
        SimulationSchedule schedule,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reproduces a minimized deterministic failure through this provider.
    /// </summary>
    Task<SimulationResult> ReproduceAsync(
        SimulationReproduction reproduction,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken);

    /// <summary>
    /// Systematically explores bounded scheduler choices through this provider.
    /// </summary>
    Task<ExplorationResult> ExploreAsync(
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken);

    /// <summary>
    /// Minimizes a deterministic failure through this provider.
    /// </summary>
    Task<MinimizationResult> MinimizeAsync(
        MinimizationOptions options,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reproduces and reduces one deterministic failure to produce semantic failure intelligence.
    /// </summary>
    Task<FailureAnalysis> AnalyzeFailureAsync(
        FailureAnalysisOptions options,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        return Simulation.AnalyzeFailureAsync(Options, options, failure, scenario, cancellationToken);
    }

    /// <summary>
    /// Executes one ordered unified verification plan through this provider.
    /// </summary>
    Task<VerificationRunResult> VerifyAsync(
        VerificationPlan plan,
        VerificationRunOptions? options,
        CancellationToken cancellationToken)
    {
        return Simulation.VerifyAsync(plan, options, cancellationToken);
    }
}
