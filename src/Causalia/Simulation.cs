using Causalia.Exceptions;
using Causalia.Minimization;
using Causalia.Runtime;
using Causalia.Scheduling;

namespace Causalia;

/// <summary>
/// Executes deterministic simulation scenarios.
/// </summary>
public static partial class Simulation
{
    /// <summary>
    /// Executes one scenario using seeded deterministic scheduling and virtual time.
    /// </summary>
    public static Task<SimulationResult> RunAsync(
        SimulationOptions options,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ValidateSimulationOptions(options);
        ArgumentNullException.ThrowIfNull(scenario);
        var scheduler = new DeterministicScheduler(options, ScheduleExecutionOptions.Random(), cancellationToken);
        return ExecuteOnWorkerAsync(() => scheduler.Run(scenario), cancellationToken);
    }

    /// <summary>
    /// Replays a previously captured branching schedule and fails if the deterministic execution shape diverges.
    /// </summary>
    public static Task<SimulationResult> ReplayAsync(
        SimulationOptions options,
        SimulationSchedule schedule,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ValidateSimulationOptions(options);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(scenario);
        var scheduler = new DeterministicScheduler(options, ScheduleExecutionOptions.Replay(schedule), cancellationToken);
        return ExecuteOnWorkerAsync(() => scheduler.Run(scenario), cancellationToken);
    }

    /// <summary>
    /// Reproduces a minimized deterministic failure using canonical scheduler choices except where explicitly forced.
    /// Naturally triggered faults not listed in the reproduction are suppressed.
    /// </summary>
    public static Task<SimulationResult> ReproduceAsync(
        SimulationOptions options,
        SimulationReproduction reproduction,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ValidateSimulationOptions(options);
        ArgumentNullException.ThrowIfNull(reproduction);
        ArgumentNullException.ThrowIfNull(scenario);

        if (options.Seed != reproduction.Seed)
        {
            throw new ArgumentException(
                $"Simulation seed {options.Seed} does not match reproduction seed {reproduction.Seed}.",
                nameof(options));
        }

        var scheduler = new DeterministicScheduler(
            options,
            ScheduleExecutionOptions.ForReproduction(reproduction),
            cancellationToken);
        return ExecuteOnWorkerAsync(() => scheduler.Run(scenario), cancellationToken);
    }

    /// <summary>
    /// Minimizes a deterministic failure by removing scheduler choices and fault occurrences that are not required to reproduce it.
    /// </summary>
    public static Task<MinimizationResult> MinimizeAsync(
        SimulationOptions options,
        MinimizationOptions minimizationOptions,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ValidateSimulationOptions(options);
        ArgumentNullException.ThrowIfNull(minimizationOptions);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(scenario);
        return ExecuteOnWorkerAsync(
            () => SimulationMinimizer.Minimize(options, minimizationOptions, failure, scenario, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Explores bounded scheduler branching choices using the configured exploration strategy.
    /// </summary>
    public static Task<ExplorationResult> ExploreAsync(
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ValidateExplorationOptions(options);
        ArgumentNullException.ThrowIfNull(scenario);

        return ExecuteOnWorkerAsync(
            () => options.Strategy switch
            {
                ExplorationStrategy.DepthFirst => ExploreDepthFirst(options, scenario, cancellationToken),
                ExplorationStrategy.CoverageGuided => ExploreCoverageGuided(options, scenario, cancellationToken),
                ExplorationStrategy.DynamicPartialOrderReduction => ExploreDpor(options, scenario, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(options), options.Strategy, "Unsupported exploration strategy.")
            },
            cancellationToken);
    }

    private static Task<TResult> ExecuteOnWorkerAsync<TResult>(Func<TResult> operation, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            operation,
            cancellationToken,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler,
            TaskScheduler.Default);
    }

    private static ExplorationResult ExploreDepthFirst(
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int> prefix = Array.Empty<int>();
        var coveragePoints = new HashSet<string>(StringComparer.Ordinal);
        var schedulesExplored = 0;
        var schedulesWithNewCoverage = 0;
        var depthLimitReached = false;

        while (schedulesExplored < options.MaxSchedules)
        {
            var result = ExecuteExplorationSchedule(options, scenario, prefix, schedulesExplored, cancellationToken);
            schedulesExplored++;
            var novelty = AddCoverage(coveragePoints, result);
            schedulesWithNewCoverage += novelty > 0 ? 1 : 0;
            depthLimitReached |= result.Schedule.Decisions.Count > options.MaxDecisionDepth;
            var nextPrefix = ScheduleExplorer.CreateNextPrefix(result.Schedule, options.MaxDecisionDepth);

            if (nextPrefix is null)
            {
                return CreateExplorationResult(
                    options.Strategy,
                    schedulesExplored,
                    true,
                    depthLimitReached,
                    coveragePoints.Count,
                    schedulesWithNewCoverage);
            }

            prefix = nextPrefix;
        }

        return CreateExplorationResult(
            options.Strategy,
            schedulesExplored,
            false,
            depthLimitReached,
            coveragePoints.Count,
            schedulesWithNewCoverage);
    }

    private static ExplorationResult ExploreCoverageGuided(
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        var coveragePoints = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new CoverageGuidedFrontier();
        IReadOnlyList<int> prefix = Array.Empty<int>();
        var schedulesExplored = 0;
        var schedulesWithNewCoverage = 0;
        var depthLimitReached = false;

        while (schedulesExplored < options.MaxSchedules)
        {
            var result = ExecuteExplorationSchedule(options, scenario, prefix, schedulesExplored, cancellationToken);
            schedulesExplored++;
            var novelty = AddCoverage(coveragePoints, result);
            schedulesWithNewCoverage += novelty > 0 ? 1 : 0;
            depthLimitReached |= result.Schedule.Decisions.Count > options.MaxDecisionDepth;
            var priority = schedulesExplored == 1 || novelty == 0 ? 0 : 1_000 + novelty;
            frontier.AddAlternatives(result.Schedule, options.MaxDecisionDepth, priority);

            if (frontier.Count == 0)
            {
                return CreateExplorationResult(
                    options.Strategy,
                    schedulesExplored,
                    true,
                    depthLimitReached,
                    coveragePoints.Count,
                    schedulesWithNewCoverage);
            }

            prefix = frontier.Dequeue();
        }

        return CreateExplorationResult(
            options.Strategy,
            schedulesExplored,
            false,
            depthLimitReached,
            coveragePoints.Count,
            schedulesWithNewCoverage);
    }

    private static ExplorationResult ExploreDpor(
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        var coveragePoints = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new DporFrontier();
        IReadOnlyList<int> prefix = Array.Empty<int>();
        var schedulesExplored = 0;
        var schedulesWithNewCoverage = 0;
        var depthLimitReached = false;
        var equivalentSchedulesPruned = 0;
        var preemptionBoundPruned = 0;
        var maximumPreemptionsObserved = 0;

        while (schedulesExplored < options.MaxSchedules)
        {
            var result = ExecuteExplorationSchedule(options, scenario, prefix, schedulesExplored, cancellationToken);
            schedulesExplored++;
            var novelty = AddCoverage(coveragePoints, result);
            schedulesWithNewCoverage += novelty > 0 ? 1 : 0;
            depthLimitReached |= result.Schedule.Decisions.Count > options.MaxDecisionDepth;

            var analysis = DporScheduleAnalyzer.Analyze(result, options);
            equivalentSchedulesPruned += analysis.EquivalentSchedulesPruned;
            preemptionBoundPruned += analysis.PreemptionBoundPruned;
            maximumPreemptionsObserved = Math.Max(maximumPreemptionsObserved, analysis.PreemptionsObserved);

            foreach (var alternative in analysis.AlternativePrefixes)
            {
                frontier.TryAdd(alternative);
            }

            if (frontier.Count == 0)
            {
                return CreateDporExplorationResult(
                    new ExplorationResultData
                    {
                        Strategy = options.Strategy,
                        SchedulesExplored = schedulesExplored,
                        ExhaustedWithinBounds = true,
                        DepthLimitReached = depthLimitReached,
                        CoveragePointsDiscovered = coveragePoints.Count,
                        SchedulesWithNewCoverage = schedulesWithNewCoverage,
                        EquivalentSchedulesPruned = equivalentSchedulesPruned,
                        PreemptionBoundPruned = preemptionBoundPruned,
                        MaximumPreemptionsObserved = maximumPreemptionsObserved
                    });
            }

            prefix = frontier.Pop();
        }

        return CreateDporExplorationResult(
            new ExplorationResultData
            {
                Strategy = options.Strategy,
                SchedulesExplored = schedulesExplored,
                ExhaustedWithinBounds = false,
                DepthLimitReached = depthLimitReached,
                CoveragePointsDiscovered = coveragePoints.Count,
                SchedulesWithNewCoverage = schedulesWithNewCoverage,
                EquivalentSchedulesPruned = equivalentSchedulesPruned,
                PreemptionBoundPruned = preemptionBoundPruned,
                MaximumPreemptionsObserved = maximumPreemptionsObserved
            });
    }

    private static SimulationResult ExecuteExplorationSchedule(
        ExplorationOptions options,
        Func<SimulationContext, Task> scenario,
        IReadOnlyList<int> prefix,
        int schedulesExplored,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scheduler = new DeterministicScheduler(
            options.Simulation,
            ScheduleExecutionOptions.Systematic(prefix, options.MaxPreemptions is not null),
            cancellationToken);

        try
        {
            return scheduler.Run(scenario);
        }
        catch (SimulationFailedException exception)
        {
            throw new SimulationExplorationFailedException(options.Strategy, schedulesExplored + 1, exception);
        }
    }

    private static int AddCoverage(HashSet<string> coveragePoints, SimulationResult result)
    {
        var added = 0;

        foreach (var point in result.Coverage.Points)
        {
            added += coveragePoints.Add(point) ? 1 : 0;
        }

        return added;
    }

    private static ExplorationResult CreateExplorationResult(
        ExplorationStrategy strategy,
        int schedulesExplored,
        bool exhaustedWithinBounds,
        bool depthLimitReached,
        int coveragePointsDiscovered,
        int schedulesWithNewCoverage)
    {
        return new ExplorationResult(
            new ExplorationResultData
            {
                Strategy = strategy,
                SchedulesExplored = schedulesExplored,
                ExhaustedWithinBounds = exhaustedWithinBounds,
                DepthLimitReached = depthLimitReached,
                CoveragePointsDiscovered = coveragePointsDiscovered,
                SchedulesWithNewCoverage = schedulesWithNewCoverage,
                EquivalentSchedulesPruned = 0,
                PreemptionBoundPruned = 0,
                MaximumPreemptionsObserved = 0
            });
    }

    private static ExplorationResult CreateDporExplorationResult(ExplorationResultData data)
    {
        return new ExplorationResult(data);
    }

    private static void ValidateExplorationOptions(ExplorationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateSimulationOptions(options.Simulation);

        if (options.MaxSchedules <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxSchedules, "MaxSchedules must be greater than zero.");
        }

        if (options.MaxDecisionDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxDecisionDepth, "MaxDecisionDepth must be greater than zero.");
        }

        if (options.MaxPreemptions is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxPreemptions, "MaxPreemptions cannot be negative.");
        }
    }

    private static void ValidateSimulationOptions(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxSteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxSteps, "MaxSteps must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(options.TimeTravel);

        if (options.TimeTravel.MaxRetainedCheckpoints <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.TimeTravel.MaxRetainedCheckpoints,
                "TimeTravel.MaxRetainedCheckpoints must be greater than zero.");
        }
    }
}
