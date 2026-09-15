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
/// Default framework-agnostic implementation of a scenario-scoped Causalia provider.
/// </summary>
public class SimulationProvider : ISimulationProvider
{
    private SimulationOptions _options = new();

    /// <inheritdoc />
    public SimulationOptions Options => _options;

    /// <inheritdoc />
    public SimulationResult? LastResult { get; private set; }

    /// <inheritdoc />
    public SimulationFailedException? LastFailure { get; private set; }

    /// <inheritdoc />
    public FailureAnalysis? LastFailureAnalysis { get; private set; }

    /// <inheritdoc />
    public TimeTravelTimeline? LastTimeTravel { get; private set; }

    /// <inheritdoc />
    public ProductionRealityEvidence? LastProductionReality { get; private set; }

    /// <inheritdoc />
    public SimulationInvariantException? LastInvariantFailure { get; private set; }

    /// <inheritdoc />
    public SimulationLinearizabilityException? LastLinearizabilityFailure { get; private set; }

    /// <inheritdoc />
    public SimulationConsistencyViolationException? LastConsistencyFailure { get; private set; }

    /// <inheritdoc />
    public SimulationModelViolationException? LastModelFailure { get; private set; }

    /// <inheritdoc />
    public ExplorationResult? LastExplorationResult { get; private set; }

    /// <inheritdoc />
    public SimulationExplorationFailedException? LastExplorationFailure { get; private set; }

    /// <inheritdoc />
    public MinimizationResult? LastMinimizationResult { get; private set; }

    /// <summary>
    /// Gets the most recent successful model-based exploration result for this provider instance.
    /// </summary>
    public ModelBasedExplorationResult? LastModelBasedResult { get; private set; }

    /// <summary>
    /// Gets the most recent failure discovered by model-based exploration.
    /// </summary>
    public SimulationModelExplorationFailedException? LastModelBasedFailure { get; private set; }

    /// <summary>
    /// Gets the most recent model command-sequence minimization result.
    /// </summary>
    public ModelBasedMinimizationResult? LastModelBasedMinimizationResult { get; private set; }

    /// <inheritdoc />
    public VerificationRunResult? LastVerificationResult { get; private set; }

    /// <inheritdoc />
    public void Configure(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SimulationResult> RunAsync(Func<SimulationContext, Task> scenario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ResetRunState();

        try
        {
            var result = await Simulation.RunAsync(_options, scenario, cancellationToken);
            LastResult = result;
            LastTimeTravel = result.TimeTravel;
            LastProductionReality = result.ProductionReality;
            return result;
        }
        catch (SimulationFailedException exception)
        {
            CaptureFailure(exception);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SimulationResult> ReplayAsync(
        SimulationSchedule schedule,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(scenario);
        ResetRunState();

        try
        {
            var result = await Simulation.ReplayAsync(_options, schedule, scenario, cancellationToken);
            LastResult = result;
            LastTimeTravel = result.TimeTravel;
            LastProductionReality = result.ProductionReality;
            return result;
        }
        catch (SimulationFailedException exception)
        {
            CaptureFailure(exception);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SimulationResult> ReproduceAsync(
        SimulationReproduction reproduction,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reproduction);
        ArgumentNullException.ThrowIfNull(scenario);
        ResetRunState();

        try
        {
            var result = await Simulation.ReproduceAsync(_options, reproduction, scenario, cancellationToken);
            LastResult = result;
            LastTimeTravel = result.TimeTravel;
            LastProductionReality = result.ProductionReality;
            return result;
        }
        catch (SimulationFailedException exception)
        {
            CaptureFailure(exception);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExplorationResult> ExploreAsync(
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scenario);
        ResetExplorationState();

        try
        {
            var result = await Simulation.ExploreAsync(options, scenario, cancellationToken);
            LastExplorationResult = result;
            return result;
        }
        catch (SimulationExplorationFailedException exception)
        {
            LastExplorationFailure = exception;
            CaptureFailure(exception.Failure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MinimizationResult> MinimizeAsync(
        MinimizationOptions options,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(scenario);
        LastMinimizationResult = null;

        var result = await Simulation.MinimizeAsync(_options, options, failure, scenario, cancellationToken);
        LastMinimizationResult = result;
        CaptureFailure(result.MinimizedFailure);
        return result;
    }

    /// <inheritdoc />
    public async Task<FailureAnalysis> AnalyzeFailureAsync(
        FailureAnalysisOptions options,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(scenario);
        LastFailureAnalysis = null;

        var analysis = await Simulation.AnalyzeFailureAsync(_options, options, failure, scenario, cancellationToken);
        LastMinimizationResult = analysis.Minimization;
        CaptureFailure(analysis.RepresentativeFailure);
        LastFailureAnalysis = analysis;
        return analysis;
    }

    /// <summary>
    /// Systematically explores executable model command sequences through this provider.
    /// </summary>
    public async Task<ModelBasedExplorationResult> CheckModelAsync<TState, TSystem>(
        ModelBasedOptions options,
        ModelBasedSpecification<TState, TSystem> specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(specification);
        ResetModelState();

        try
        {
            var result = await Simulation.CheckModelAsync(options, specification, cancellationToken);
            LastModelBasedResult = result;
            return result;
        }
        catch (SimulationModelExplorationFailedException exception)
        {
            LastModelBasedFailure = exception;
            CaptureFailure(exception.Failure);
            throw;
        }
    }

    /// <summary>
    /// Replays one model command sequence through this provider.
    /// </summary>
    public async Task<ModelBasedReplayResult<TState>> ReplayModelAsync<TState, TSystem>(
        ModelBasedReplayOptions options,
        ModelSequence sequence,
        ModelBasedSpecification<TState, TSystem> specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(specification);
        ResetRunState();

        try
        {
            var result = await Simulation.ReplayModelAsync(options, sequence, specification, cancellationToken);
            LastResult = result.Simulation;
            LastTimeTravel = result.Simulation.TimeTravel;
            LastProductionReality = result.Simulation.ProductionReality;
            return result;
        }
        catch (SimulationFailedException exception)
        {
            CaptureFailure(exception);
            throw;
        }
    }

    /// <summary>
    /// Minimizes one failing model command sequence through this provider.
    /// </summary>
    public async Task<ModelBasedMinimizationResult> MinimizeModelAsync<TState, TSystem>(
        ModelBasedOptions options,
        ModelBasedMinimizationOptions minimizationOptions,
        ModelSequence sequence,
        ModelBasedSpecification<TState, TSystem> specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(minimizationOptions);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(specification);
        LastModelBasedMinimizationResult = null;
        LastVerificationResult = null;

        var result = await Simulation.MinimizeModelAsync(options, minimizationOptions, sequence, specification, cancellationToken);
        LastModelBasedMinimizationResult = result;
        CaptureFailure(result.Failure);
        return result;
    }

    /// <inheritdoc />
    public async Task<VerificationRunResult> VerifyAsync(
        VerificationPlan plan,
        VerificationRunOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ResetRunState();
        var result = await Simulation.VerifyAsync(plan, options, cancellationToken);
        LastVerificationResult = result;

        var latestFailure = result.Steps.LastOrDefault(value => value.Failure is not null)?.Failure;
        if (latestFailure is not null)
        {
            CaptureFailure(latestFailure);
            LastVerificationResult = result;
        }

        return result;
    }

    private void CaptureFailure(SimulationFailedException exception)
    {
        LastFailure = exception;
        LastTimeTravel = exception.TimeTravel;
        LastProductionReality = exception.ProductionReality;
        LastFailureAnalysis = FailureAnalyzer.Describe(exception);
        LastInvariantFailure = exception.InvariantFailure;
        LastLinearizabilityFailure = exception.LinearizabilityFailure;
        LastConsistencyFailure = exception.ConsistencyFailure;
        LastModelFailure = exception.ModelFailure;
    }

    private void ResetRunState()
    {
        LastResult = null;
        LastFailure = null;
        LastFailureAnalysis = null;
        LastTimeTravel = null;
        LastProductionReality = null;
        LastInvariantFailure = null;
        LastLinearizabilityFailure = null;
        LastConsistencyFailure = null;
        LastModelFailure = null;
        LastMinimizationResult = null;
        LastModelBasedResult = null;
        LastModelBasedFailure = null;
        LastModelBasedMinimizationResult = null;
        LastVerificationResult = null;
    }

    private void ResetModelState()
    {
        ResetRunState();
        LastExplorationResult = null;
        LastExplorationFailure = null;
    }

    private void ResetExplorationState()
    {
        LastExplorationResult = null;
        LastExplorationFailure = null;
        LastFailure = null;
        LastFailureAnalysis = null;
        LastTimeTravel = null;
        LastProductionReality = null;
        LastInvariantFailure = null;
        LastLinearizabilityFailure = null;
        LastConsistencyFailure = null;
        LastModelFailure = null;
        LastMinimizationResult = null;
        LastModelBasedResult = null;
        LastModelBasedFailure = null;
        LastModelBasedMinimizationResult = null;
        LastVerificationResult = null;
    }
}
