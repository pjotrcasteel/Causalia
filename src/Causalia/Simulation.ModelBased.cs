using Causalia.Exceptions;
using Causalia.ModelBased;
using Causalia.Scheduling;

namespace Causalia;

public static partial class Simulation
{
    /// <summary>
    /// Systematically explores bounded executable-model command sequences and optionally explores scheduler timelines for every sequence.
    /// </summary>
    public static async Task<ModelBasedExplorationResult> CheckModelAsync<TState, TSystem>(
        ModelBasedOptions options,
        ModelBasedSpecification<TState, TSystem> specification,
        CancellationToken cancellationToken)
    {
        ValidateModelBasedOptions(options);
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var initialState = specification.CreateInitialState();
        var initialCommands = specification.GetEnabledCommands(initialState);
        var frontier = new Stack<ModelSequence>();
        PushExtensions(frontier, new ModelSequence([]), initialCommands);
        var sequencesExplored = 0;
        var schedulesExplored = 0;
        long commandsExecuted = 0;
        var maximumDepthReached = 0;
        var depthLimitReached = false;

        while (frontier.Count > 0 && sequencesExplored < options.MaxSequences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = frontier.Pop();
            var resolved = ResolveModelSequence(specification, sequence);
            sequencesExplored++;
            maximumDepthReached = Math.Max(maximumDepthReached, sequence.Commands.Count);

            var execution = await ExecuteModelSequenceAsync(options, specification, sequence, cancellationToken);
            schedulesExplored = checked(schedulesExplored + execution.SchedulesExplored);
            commandsExecuted = checked(commandsExecuted + ((long)sequence.Commands.Count * execution.SchedulesExplored));

            if (execution.Failure is not null)
            {
                throw new SimulationModelExplorationFailedException(
                    specification.Name,
                    sequencesExplored,
                    schedulesExplored,
                    sequence,
                    execution.Failure);
            }

            if (sequence.Commands.Count >= options.MaxCommandDepth)
            {
                depthLimitReached |= resolved.EnabledCommands.Count > 0;
                continue;
            }

            PushExtensions(frontier, sequence, resolved.EnabledCommands);
        }

        return new ModelBasedExplorationResult(
            new ModelBasedExplorationResultData
            {
                SequencesExplored = sequencesExplored,
                CommandsExecuted = commandsExecuted,
                SchedulesExplored = schedulesExplored,
                MaximumDepthReached = maximumDepthReached,
                ExhaustedWithinBounds = frontier.Count == 0,
                DepthLimitReached = depthLimitReached
            });
    }

    /// <summary>
    /// Replays one exact model command sequence and optionally one exact scheduler schedule.
    /// </summary>
    public static async Task<ModelBasedReplayResult<TState>> ReplayModelAsync<TState, TSystem>(
        ModelBasedReplayOptions options,
        ModelSequence sequence,
        ModelBasedSpecification<TState, TSystem> specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateSimulationOptions(options.Simulation);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = ResolveModelSequence(specification, sequence);
        var scenario = CreateModelScenario(specification, sequence);
        var result = options.Schedule is null
            ? await RunAsync(options.Simulation, scenario, cancellationToken)
            : await ReplayAsync(options.Simulation, options.Schedule, scenario, cancellationToken);
        return new ModelBasedReplayResult<TState>(sequence, resolved.FinalState, result);
    }

    /// <summary>
    /// Reduces a failing model command sequence by removing contiguous command ranges while preserving the same failure class.
    /// </summary>
    public static async Task<ModelBasedMinimizationResult> MinimizeModelAsync<TState, TSystem>(
        ModelBasedOptions options,
        ModelBasedMinimizationOptions minimizationOptions,
        ModelSequence sequence,
        ModelBasedSpecification<TState, TSystem> specification,
        CancellationToken cancellationToken)
    {
        ValidateModelBasedOptions(options);
        ArgumentNullException.ThrowIfNull(minimizationOptions);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(specification);

        if (minimizationOptions.MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimizationOptions),
                minimizationOptions.MaxAttempts,
                "MaxAttempts must be greater than zero.");
        }

        if (sequence.Commands.Count == 0)
        {
            throw new ArgumentException("A failing model sequence must contain at least one command.", nameof(sequence));
        }

        ResolveModelSequence(specification, sequence);
        var originalExecution = await ExecuteModelSequenceAsync(options, specification, sequence, cancellationToken);
        var originalFailure = originalExecution.Failure
            ?? throw new ArgumentException("The supplied model sequence does not fail under the configured execution options.", nameof(sequence));
        var current = sequence;
        var currentFailure = originalFailure;
        var attempts = 0;
        var granularity = 2;

        while (current.Commands.Count > 1 && attempts < minimizationOptions.MaxAttempts)
        {
            var chunkSize = (int)Math.Ceiling((double)current.Commands.Count / granularity);
            var reduced = false;

            for (var start = 0; start < current.Commands.Count && attempts < minimizationOptions.MaxAttempts; start += chunkSize)
            {
                var count = Math.Min(chunkSize, current.Commands.Count - start);
                var candidate = current.RemoveRange(start, count);
                if (candidate.Commands.Count == 0 || !CanResolveModelSequence(specification, candidate))
                {
                    continue;
                }

                attempts++;
                var execution = await ExecuteModelSequenceAsync(options, specification, candidate, cancellationToken);
                if (execution.Failure is null || !HasEquivalentFailure(originalFailure, execution.Failure))
                {
                    continue;
                }

                current = candidate;
                currentFailure = execution.Failure;
                granularity = Math.Max(2, granularity - 1);
                reduced = true;
                break;
            }

            if (reduced)
            {
                continue;
            }

            if (granularity >= current.Commands.Count)
            {
                break;
            }

            granularity = Math.Min(current.Commands.Count, granularity * 2);
        }

        return new ModelBasedMinimizationResult(sequence, current, attempts, currentFailure);
    }

    private static async Task<ModelSequenceExecutionResult> ExecuteModelSequenceAsync<TState, TSystem>(
        ModelBasedOptions options,
        ModelBasedSpecification<TState, TSystem> specification,
        ModelSequence sequence,
        CancellationToken cancellationToken)
    {
        var scenario = CreateModelScenario(specification, sequence);

        if (options.ScheduleExploration is null)
        {
            try
            {
                await RunAsync(options.Simulation, scenario, cancellationToken);
                return new ModelSequenceExecutionResult(1, null);
            }
            catch (SimulationFailedException exception)
            {
                return new ModelSequenceExecutionResult(1, exception);
            }
        }

        var explorationOptions = CreateScheduleExplorationOptions(options);
        try
        {
            var result = await ExploreAsync(explorationOptions, scenario, cancellationToken);
            return new ModelSequenceExecutionResult(result.SchedulesExplored, null);
        }
        catch (SimulationExplorationFailedException exception)
        {
            return new ModelSequenceExecutionResult(exception.SchedulesExplored, exception.Failure);
        }
    }

    private static Func<SimulationContext, Task> CreateModelScenario<TState, TSystem>(
        ModelBasedSpecification<TState, TSystem> specification,
        ModelSequence sequence)
    {
        return async context =>
        {
            var state = specification.CreateInitialState();
            var system = await specification.CreateSystemAsync(context, context.CancellationToken);
            if (system is null)
            {
                throw new InvalidOperationException($"Model '{specification.Name}' system factory returned null.");
            }

            try
            {
                for (var index = 0; index < sequence.Commands.Count; index++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    var command = ResolveCommand(specification, state, sequence.Commands[index], index);
                    var expected = command.Transition(state);
                    context.TraceEvent($"model:{specification.Name}:command:{index}:start:{command.Name}");
                    context.Coverage.RecordAutomatic($"model:{specification.Name}:command:{command.Name}");
                    var verification = await command.ExecuteAndVerifyAsync(
                        state,
                        expected,
                        system,
                        context,
                        context.CancellationToken);

                    if (!verification.Succeeded)
                    {
                        context.TraceEvent($"model:{specification.Name}:command:{index}:failed:{command.Name}");
                        var prefix = new ModelSequence(sequence.Commands.Take(index + 1).ToList());
                        throw new SimulationModelViolationException(
                            specification.Name,
                            command.Name,
                            index,
                            prefix,
                            verification.Message ?? "Model verification failed without a diagnostic message.");
                    }

                    context.TraceEvent($"model:{specification.Name}:command:{index}:verified:{command.Name}");
                    state = expected;
                }
            }
            finally
            {
                await DisposeSystemAsync(system);
            }
        };
    }

    private static ResolvedModelSequence<TState, TSystem> ResolveModelSequence<TState, TSystem>(
        ModelBasedSpecification<TState, TSystem> specification,
        ModelSequence sequence)
    {
        var state = specification.CreateInitialState();

        for (var index = 0; index < sequence.Commands.Count; index++)
        {
            var command = ResolveCommand(specification, state, sequence.Commands[index], index);
            state = command.Transition(state);
        }

        return new ResolvedModelSequence<TState, TSystem>(state, specification.GetEnabledCommands(state));
    }

    private static ModelCommand<TState, TSystem> ResolveCommand<TState, TSystem>(
        ModelBasedSpecification<TState, TSystem> specification,
        TState state,
        string commandName,
        int index)
    {
        var command = specification.GetEnabledCommands(state).FirstOrDefault(
            candidate => string.Equals(candidate.Name, commandName, StringComparison.Ordinal));
        return command ?? throw new SimulationModelReplayException(
            $"Model '{specification.Name}' replay diverged at command {index}. " +
            $"Command '{commandName}' is not enabled in the reconstructed model state.");
    }

    private static bool CanResolveModelSequence<TState, TSystem>(
        ModelBasedSpecification<TState, TSystem> specification,
        ModelSequence sequence)
    {
        try
        {
            ResolveModelSequence(specification, sequence);
            return true;
        }
        catch (SimulationModelReplayException)
        {
            return false;
        }
    }

    private static void PushExtensions<TState, TSystem>(
        Stack<ModelSequence> frontier,
        ModelSequence prefix,
        IReadOnlyList<ModelCommand<TState, TSystem>> commands)
    {
        for (var index = commands.Count - 1; index >= 0; index--)
        {
            frontier.Push(prefix.Append(commands[index].Name));
        }
    }

    private static ExplorationOptions CreateScheduleExplorationOptions(ModelBasedOptions options)
    {
        var schedule = options.ScheduleExploration!;
        return new ExplorationOptions
        {
            Strategy = schedule.Strategy,
            Simulation = options.Simulation,
            MaxSchedules = schedule.MaxSchedules,
            MaxDecisionDepth = schedule.MaxDecisionDepth,
            MaxPreemptions = schedule.MaxPreemptions
        };
    }

    private static void ValidateModelBasedOptions(ModelBasedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateSimulationOptions(options.Simulation);

        if (options.MaxSequences <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxSequences, "MaxSequences must be greater than zero.");
        }

        if (options.MaxCommandDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxCommandDepth, "MaxCommandDepth must be greater than zero.");
        }

        if (options.ScheduleExploration is null)
        {
            return;
        }

        if (options.ScheduleExploration.MaxSchedules <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Schedule exploration MaxSchedules must be greater than zero.");
        }

        if (options.ScheduleExploration.MaxDecisionDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Schedule exploration MaxDecisionDepth must be greater than zero.");
        }

        if (options.ScheduleExploration.MaxPreemptions is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Schedule exploration MaxPreemptions cannot be negative.");
        }
    }

    private static bool HasEquivalentFailure(SimulationFailedException original, SimulationFailedException candidate)
    {
        if (original.InnerException is not { } originalInner || candidate.InnerException is not { } candidateInner)
        {
            return false;
        }

        if (originalInner.GetType() != candidateInner.GetType())
        {
            return false;
        }

        if (originalInner is not SimulationModelViolationException originalModel ||
            candidateInner is not SimulationModelViolationException candidateModel)
        {
            return true;
        }

        return string.Equals(originalModel.ModelName, candidateModel.ModelName, StringComparison.Ordinal) &&
            string.Equals(originalModel.CommandName, candidateModel.CommandName, StringComparison.Ordinal);
    }

    private static async ValueTask DisposeSystemAsync<TSystem>(TSystem system)
    {
        if (system is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        if (system is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private sealed record ModelSequenceExecutionResult(int SchedulesExplored, SimulationFailedException? Failure);

    private sealed record ResolvedModelSequence<TState, TSystem>(
        TState FinalState,
        IReadOnlyList<ModelCommand<TState, TSystem>> EnabledCommands);
}
