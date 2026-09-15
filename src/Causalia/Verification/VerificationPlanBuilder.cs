using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.ModelBased;
using Causalia.Scheduling;

namespace Causalia.Verification;

/// <summary>
/// Builds an ordered verification plan from deterministic simulation engines.
/// </summary>
public sealed class VerificationPlanBuilder
{
    private readonly List<VerificationStepDefinition> _steps = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds one seeded deterministic simulation step.
    /// </summary>
    public VerificationPlanBuilder AddRun(
        string name,
        SimulationOptions options,
        Func<SimulationContext, Task> scenario)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scenario);
        AddStepName(name);
        _steps.Add(
            new VerificationStepDefinition
            {
                Name = name,
                Kind = VerificationStepKind.Simulation,
                Seed = options.Seed,
                ExecuteAsync = (runOptions, cancellationToken) => ExecuteRunAsync(
                    name,
                    options,
                    scenario,
                    runOptions,
                    cancellationToken)
            });
        return this;
    }

    /// <summary>
    /// Adds one bounded scheduler-exploration step.
    /// </summary>
    public VerificationPlanBuilder AddExploration(
        string name,
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scenario);
        AddStepName(name);
        _steps.Add(
            new VerificationStepDefinition
            {
                Name = name,
                Kind = VerificationStepKind.ScheduleExploration,
                Seed = options.Simulation.Seed,
                ExecuteAsync = (runOptions, cancellationToken) => ExecuteExplorationAsync(
                    name,
                    options,
                    scenario,
                    runOptions,
                    cancellationToken)
            });
        return this;
    }

    /// <summary>
    /// Adds one bounded executable model-based verification step.
    /// </summary>
    public VerificationPlanBuilder AddModel<TState, TSystem>(
        string name,
        ModelBasedOptions options,
        ModelBasedSpecification<TState, TSystem> specification)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(specification);
        AddStepName(name);
        _steps.Add(
            new VerificationStepDefinition
            {
                Name = name,
                Kind = VerificationStepKind.ModelBased,
                Seed = options.Simulation.Seed,
                ExecuteAsync = (runOptions, cancellationToken) => ExecuteModelAsync(
                    name,
                    options,
                    specification,
                    runOptions,
                    cancellationToken)
            });
        return this;
    }

    internal VerificationPlan Build(string name)
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("A verification plan must contain at least one step.");
        }

        return new VerificationPlan(name, _steps.ToList().AsReadOnly());
    }

    private void AddStepName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_names.Add(name))
        {
            throw new InvalidOperationException($"A verification step named '{name}' already exists in this plan.");
        }
    }

    private static async Task<VerificationStepResult> ExecuteRunAsync(
        string name,
        SimulationOptions options,
        Func<SimulationContext, Task> scenario,
        VerificationRunOptions runOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await Simulation.RunAsync(options, scenario, cancellationToken);
            return CreatePassedRun(name, options.Seed, result);
        }
        catch (SimulationFailedException failure)
        {
            var analysis = await AnalyzeAsync(options, scenario, failure, runOptions, cancellationToken);
            return CreateFailed(name, VerificationStepKind.Simulation, options.Seed, failure, analysis);
        }
    }

    private static async Task<VerificationStepResult> ExecuteExplorationAsync(
        string name,
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        VerificationRunOptions runOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await Simulation.ExploreAsync(options, scenario, cancellationToken);
            return CreatePassedExploration(name, options.Simulation.Seed, result);
        }
        catch (SimulationExplorationFailedException failure)
        {
            var analysis = await AnalyzeAsync(options.Simulation, scenario, failure.Failure, runOptions, cancellationToken);
            return CreateFailed(name, VerificationStepKind.ScheduleExploration, options.Simulation.Seed, failure.Failure, analysis);
        }
    }

    private static async Task<VerificationStepResult> ExecuteModelAsync<TState, TSystem>(
        string name,
        ModelBasedOptions options,
        ModelBasedSpecification<TState, TSystem> specification,
        VerificationRunOptions runOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await Simulation.CheckModelAsync(options, specification, cancellationToken);
            return new VerificationStepResult(
                new VerificationStepResultData
                {
                    Name = name,
                    Kind = VerificationStepKind.ModelBased,
                    Status = VerificationStepStatus.Passed,
                    Seed = options.Simulation.Seed,
                    ModelBased = result
                });
        }
        catch (SimulationModelExplorationFailedException failure)
        {
            var analysis = FailureAnalyzer.Describe(failure.Failure, runOptions.TraceContextEntries);
            return CreateFailed(name, VerificationStepKind.ModelBased, options.Simulation.Seed, failure.Failure, analysis);
        }
    }

    private static Task<FailureAnalysis> AnalyzeAsync(
        SimulationOptions options,
        Func<SimulationContext, Task> scenario,
        SimulationFailedException failure,
        VerificationRunOptions runOptions,
        CancellationToken cancellationToken)
    {
        if (!runOptions.MinimizeFailures)
        {
            return Task.FromResult(FailureAnalyzer.Describe(failure, runOptions.TraceContextEntries));
        }

        return Simulation.AnalyzeFailureAsync(options, runOptions.FailureAnalysis, failure, scenario, cancellationToken);
    }

    private static VerificationStepResult CreatePassedRun(string name, ulong seed, SimulationResult result)
    {
        return new VerificationStepResult(
            new VerificationStepResultData
            {
                Name = name,
                Kind = VerificationStepKind.Simulation,
                Status = VerificationStepStatus.Passed,
                Seed = seed,
                Simulation = result
            });
    }

    private static VerificationStepResult CreatePassedExploration(string name, ulong seed, ExplorationResult result)
    {
        return new VerificationStepResult(
            new VerificationStepResultData
            {
                Name = name,
                Kind = VerificationStepKind.ScheduleExploration,
                Status = VerificationStepStatus.Passed,
                Seed = seed,
                Exploration = result
            });
    }

    private static VerificationStepResult CreateFailed(
        string name,
        VerificationStepKind kind,
        ulong seed,
        SimulationFailedException failure,
        FailureAnalysis analysis)
    {
        return new VerificationStepResult(
            new VerificationStepResultData
            {
                Name = name,
                Kind = kind,
                Status = VerificationStepStatus.Failed,
                Seed = seed,
                Failure = failure,
                FailureAnalysis = analysis
            });
    }
}
