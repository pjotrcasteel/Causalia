using Causalia.Consistency;
using Causalia.Coverage;
using Causalia.Exceptions;
using Causalia.Exploration;
using Causalia.Invariants;
using Causalia.Randomness;
using Causalia.ProductionReality;
using Causalia.Scheduling;
using Causalia.Time;
using Causalia.Tracing;
using Causalia.TimeTravel;

namespace Causalia.Runtime;

internal sealed class DeterministicScheduler
{
    private readonly CancellationToken _cancellationToken;
    private readonly DeterministicRandom _random;
    private readonly List<ScheduledWorkItem> _runnable = new();
    private readonly List<ExplorationDecisionMetadata> _explorationDecisions = new();
    private readonly Dictionary<long, ExplorationOperationRegistration> _explorationOperations = new();
    private readonly List<ScheduleDecision> _scheduleDecisions = new();
    private readonly List<SimulationTraceEntry> _trace = new();
    private readonly List<Task> _trackedOperations = new();
    private readonly SimulationOptions _options;
    private readonly ScheduleExecutionOptions _scheduleExecution;
    private readonly SimulationSynchronizationContext _synchronizationContext;
    private long _nextWorkItemId;
    private long _nextExplorationOperationId;
    private long? _currentWorkItemId;
    private long? _currentOperationId;
    private long? _lastDecisionOperationId;
    private int _steps;

    public DeterministicScheduler(
        SimulationOptions options,
        ScheduleExecutionOptions scheduleExecution,
        CancellationToken cancellationToken)
    {
        _options = options;
        _scheduleExecution = scheduleExecution;
        _cancellationToken = cancellationToken;
        _random = new DeterministicRandom(options.Seed);
        _synchronizationContext = new SimulationSynchronizationContext(this);
        Faults = new FaultExecutionController(scheduleExecution.Reproduction);
        Coverage = new SimulationCoverage();
        Random = new SimulationRandom(options.Seed ^ 0xA0761D6478BD642FUL);
        TimeProvider = new SimulationTimeProvider(this, options.StartTime);
        Invariants = new SimulationInvariants(this);
        Consistency = new SimulationConsistency(this);
        TimeTravel = new SimulationTimeTravel(this, options.TimeTravel);
        ProductionReality = new SimulationProductionReality(this, options.Seed);
    }

    public ulong Seed => _options.Seed;

    public SimulationTimeProvider TimeProvider { get; }

    public FaultExecutionController Faults { get; }

    public SimulationCoverage Coverage { get; }

    public SimulationRandom Random { get; }

    public SimulationInvariants Invariants { get; }

    public SimulationConsistency Consistency { get; }

    public SimulationTimeTravel TimeTravel { get; }

    public SimulationProductionReality ProductionReality { get; }

    internal int StepCount => _steps;

    internal long? CurrentExplorationOperationId => _currentOperationId;

    public IReadOnlyList<SimulationTraceEntry> Trace => _trace.AsReadOnly();

    public SimulationResult Run(Func<SimulationContext, Task> scenario)
    {
        var previousSynchronizationContext = SynchronizationContext.Current;
        SimulationContext? context = null;
        SynchronizationContext.SetSynchronizationContext(_synchronizationContext);

        try
        {
            _cancellationToken.ThrowIfCancellationRequested();
            context = new SimulationContext(this, _cancellationToken);
            var scenarioTask = StartScenario(scenario, context);
            Invariants.Observe();
            TimeTravel.CaptureAutomatic(TimeTravelCheckpointKind.Start);

            while (!scenarioTask.IsCompleted || HasPendingTrackedOperations())
            {
                _cancellationToken.ThrowIfCancellationRequested();
                ThrowIfTrackedOperationFailed();

                if (scenarioTask.IsCompleted)
                {
                    CompleteScenario(scenarioTask);
                }

                ExecuteNextStep();
            }

            CompleteScenario(scenarioTask);
            ThrowIfTrackedOperationFailed();
            var invariantOutcomes = Invariants.Complete();
            var consistencyOutcomes = Consistency.Complete();
            ValidateScheduleCompletion();
            TimeTravel.CaptureAutomatic(TimeTravelCheckpointKind.Completed);
            return CreateResult(invariantOutcomes, consistencyOutcomes);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SimulationFailedException)
        {
            throw;
        }
        catch (SimulationScheduleReplayException)
        {
            throw;
        }
        catch (SimulationReproductionDivergedException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateFailure(exception);
        }
        finally
        {
            context?.Dispose();
            SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
        }
    }

    public void Enqueue(SendOrPostCallback callback, object? state)
    {
        Enqueue(callback, state, _currentOperationId);
    }

    public long RegisterExplorationOperation(string name, IReadOnlyList<ExplorationResourceAccess> accesses)
    {
        var operationId = checked(++_nextExplorationOperationId);
        _explorationOperations.Add(
            operationId,
            new ExplorationOperationRegistration
            {
                Id = operationId,
                Name = name,
                Accesses = new List<ExplorationResourceAccess>(accesses).AsReadOnly()
            });
        return operationId;
    }

    public Task StartExplorationOperation(
        long operationId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var previousOperationId = _currentOperationId;
        _currentOperationId = operationId;

        try
        {
            return operation(cancellationToken)
                ?? throw new InvalidOperationException("The exploration operation returned a null task.");
        }
        finally
        {
            _currentOperationId = previousOperationId;
        }
    }

    public void EnqueueTimer(ScheduledTimerInvocation invocation)
    {
        Enqueue(
            static state =>
            {
                var scheduledInvocation = (ScheduledTimerInvocation)state!;
                scheduledInvocation.Timer.Fire(scheduledInvocation.Generation);
            },
            invocation,
            invocation.OperationId);
    }

    public void RecordTrace(string message)
    {
        _trace.Add(new SimulationTraceEntry(_steps, TimeProvider.GetUtcNow(), message));
    }

    public void TrackOperation(Task operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _trackedOperations.Add(operation);
    }


    private void Enqueue(SendOrPostCallback callback, object? state, long? operationId)
    {
        _runnable.Add(
            new ScheduledWorkItem(
                _nextWorkItemId++,
                callback,
                state,
                _currentWorkItemId,
                operationId));
    }

    private bool HasPendingTrackedOperations()
    {
        RemoveCompletedTrackedOperations();
        return _trackedOperations.Count > 0;
    }

    private void ThrowIfTrackedOperationFailed()
    {
        foreach (var operation in _trackedOperations)
        {
            if (operation.IsFaulted)
            {
                var exception = operation.Exception?.InnerException
                    ?? (Exception?)operation.Exception
                    ?? new InvalidOperationException("A tracked simulation operation failed.");
                throw CreateFailure(exception);
            }

            if (operation.IsCanceled)
            {
                throw new TaskCanceledException(operation);
            }
        }
    }

    private void RemoveCompletedTrackedOperations()
    {
        for (var index = _trackedOperations.Count - 1; index >= 0; index--)
        {
            var operation = _trackedOperations[index];

            if (operation.IsCompletedSuccessfully)
            {
                _trackedOperations.RemoveAt(index);
            }
        }
    }

    private Task StartScenario(Func<SimulationContext, Task> scenario, SimulationContext context)
    {
        try
        {
            return scenario(context) ?? throw new InvalidOperationException("The simulation scenario returned a null task.");
        }
        catch (Exception exception)
        {
            throw CreateFailure(exception);
        }
    }

    private void ExecuteNextStep()
    {
        if (_steps >= _options.MaxSteps)
        {
            throw new SimulationStepLimitExceededException(_options.MaxSteps);
        }

        if (_runnable.Count == 0)
        {
            Invariants.ThrowIfExpiredAtQuiescence();

            if (!TimeProvider.TryAdvanceToNextTimer())
            {
                throw new SimulationDeadlockException();
            }
        }

        if (_runnable.Count == 0)
        {
            return;
        }

        TimeTravel.CaptureAutomatic(TimeTravelCheckpointKind.BeforeSchedulerStep);
        var selectedIndex = SelectRunnableIndex();
        var candidateCount = _runnable.Count;
        var candidateOperationIds = candidateCount > 1
            ? _runnable.Select(candidate => candidate.OperationId).ToArray()
            : null;
        var workItem = _runnable[selectedIndex];
        _runnable.RemoveAt(selectedIndex);
        _steps++;

        if (candidateCount > 1)
        {
            RecordScheduleDecision(selectedIndex, candidateCount, workItem.Id);
            RecordExplorationDecision(selectedIndex, candidateOperationIds!);
            _lastDecisionOperationId = workItem.OperationId;
            Coverage.RecordAutomatic($"scheduler:branch:{_scheduleDecisions.Count - 1}:candidates:{candidateCount}");
        }

        if (_options.TraceSchedulerEvents)
        {
            RecordTrace($"scheduler:work-item:{workItem.Id}");
        }

        var previousWorkItemId = _currentWorkItemId;
        var previousOperationId = _currentOperationId;
        _currentWorkItemId = workItem.Id;
        _currentOperationId = workItem.OperationId;

        try
        {
            workItem.Callback(workItem.State);
        }
        finally
        {
            _currentWorkItemId = previousWorkItemId;
            _currentOperationId = previousOperationId;
        }

        Invariants.Observe();
        TimeTravel.CaptureAutomatic(TimeTravelCheckpointKind.AfterSchedulerStep);
    }

    private int SelectRunnableIndex()
    {
        if (_runnable.Count == 1)
        {
            return 0;
        }

        var decisionIndex = _scheduleDecisions.Count;

        return _scheduleExecution.Mode switch
        {
            ScheduleExecutionMode.Random => _random.NextInt32(_runnable.Count),
            ScheduleExecutionMode.Systematic => SelectSystematicIndex(decisionIndex),
            ScheduleExecutionMode.Replay => SelectReplayIndex(decisionIndex),
            ScheduleExecutionMode.Reproduction => SelectReproductionIndex(decisionIndex),
            _ => throw new InvalidOperationException($"Unsupported schedule mode '{_scheduleExecution.Mode}'.")
        };
    }

    private int SelectSystematicIndex(int decisionIndex)
    {
        if (decisionIndex >= _scheduleExecution.SystematicPrefix.Count)
        {
            if (_scheduleExecution.PreferNonPreemptiveChoices && _lastDecisionOperationId is not null)
            {
                var matchingIndex = _runnable.FindIndex(candidate => candidate.OperationId == _lastDecisionOperationId);

                if (matchingIndex >= 0)
                {
                    return matchingIndex;
                }
            }

            return 0;
        }

        var selectedIndex = _scheduleExecution.SystematicPrefix[decisionIndex];

        if (selectedIndex < 0 || selectedIndex >= _runnable.Count)
        {
            throw new SimulationScheduleReplayException(
                $"Systematic schedule prefix diverged at decision {decisionIndex}. " +
                $"Choice {selectedIndex} is invalid for {_runnable.Count} runnable candidates.");
        }

        return selectedIndex;
    }

    private int SelectReplayIndex(int decisionIndex)
    {
        var schedule = _scheduleExecution.ReplaySchedule!;

        if (decisionIndex >= schedule.Decisions.Count)
        {
            throw new SimulationScheduleReplayException(
                $"Replay diverged at scheduler step {_steps + 1}: the execution produced an unexpected branching decision.");
        }

        var expected = schedule.Decisions[decisionIndex];

        if (expected.DecisionIndex != decisionIndex || expected.Step != _steps + 1 || expected.CandidateCount != _runnable.Count)
        {
            throw new SimulationScheduleReplayException(
                $"Replay diverged at decision {decisionIndex}. Expected step {expected.Step} with {expected.CandidateCount} candidates, " +
                $"but observed step {_steps + 1} with {_runnable.Count} candidates.");
        }

        if (expected.SelectedIndex < 0 || expected.SelectedIndex >= _runnable.Count)
        {
            throw new SimulationScheduleReplayException(
                $"Replay decision {decisionIndex} selects invalid candidate index {expected.SelectedIndex}.");
        }

        var workItem = _runnable[expected.SelectedIndex];

        if (workItem.Id != expected.WorkItemId)
        {
            throw new SimulationScheduleReplayException(
                $"Replay diverged at decision {decisionIndex}. Expected work item {expected.WorkItemId}, but observed {workItem.Id}.");
        }

        return expected.SelectedIndex;
    }


    private int SelectReproductionIndex(int decisionIndex)
    {
        var reproduction = _scheduleExecution.Reproduction!;

        if (!reproduction.TryGetSchedulerChoice(decisionIndex, out var selectedIndex))
        {
            return 0;
        }

        if (selectedIndex < 0 || selectedIndex >= _runnable.Count)
        {
            throw new SimulationReproductionDivergedException(
                $"Minimized reproduction diverged at decision {decisionIndex}. " +
                $"Choice {selectedIndex} is invalid for {_runnable.Count} runnable candidates.");
        }

        return selectedIndex;
    }

    private void RecordScheduleDecision(int selectedIndex, int candidateCount, long workItemId)
    {
        var decision = new ScheduleDecision(_scheduleDecisions.Count, _steps, selectedIndex, candidateCount, workItemId);
        _scheduleDecisions.Add(decision);
        if (_options.TraceSchedulerEvents)
        {
            RecordTrace(
                $"scheduler:decision:{decision.DecisionIndex}:selected:{selectedIndex}:candidates:{candidateCount}:work-item:{workItemId}");
        }
    }


    private void RecordExplorationDecision(int selectedIndex, IReadOnlyList<long?> candidateOperationIds)
    {
        _explorationDecisions.Add(
            new ExplorationDecisionMetadata
            {
                DecisionIndex = _explorationDecisions.Count,
                CandidateOperationIds = new List<long?>(candidateOperationIds).AsReadOnly(),
                SelectedIndex = selectedIndex
            });
    }

    private void ValidateScheduleCompletion()
    {
        if (_scheduleExecution.Mode == ScheduleExecutionMode.Replay &&
            _scheduleDecisions.Count != _scheduleExecution.ReplaySchedule!.Decisions.Count)
        {
            throw new SimulationScheduleReplayException(
                $"Replay ended after {_scheduleDecisions.Count} branching decisions, but the schedule contains " +
                $"{_scheduleExecution.ReplaySchedule.Decisions.Count} decisions.");
        }

        if (_scheduleExecution.Mode == ScheduleExecutionMode.Systematic &&
            _scheduleDecisions.Count < _scheduleExecution.SystematicPrefix.Count)
        {
            throw new SimulationScheduleReplayException(
                $"Systematic schedule prefix contains {_scheduleExecution.SystematicPrefix.Count} decisions, " +
                $"but execution produced only {_scheduleDecisions.Count} branching decisions.");
        }
    }

    private void CompleteScenario(Task scenarioTask)
    {
        if (scenarioTask.IsFaulted)
        {
            var exception = scenarioTask.Exception?.InnerException
                ?? (Exception?)scenarioTask.Exception
                ?? new InvalidOperationException("The simulation scenario failed.");
            throw CreateFailure(exception);
        }

        if (scenarioTask.IsCanceled)
        {
            throw new TaskCanceledException(scenarioTask);
        }
    }

    private SimulationResult CreateResult(IReadOnlyList<InvariantOutcome> invariantOutcomes, IReadOnlyList<ConsistencyOutcome> consistencyOutcomes)
    {
        var trace = new List<SimulationTraceEntry>(_trace).AsReadOnly();
        var coverage = CreateCoverageSnapshot();
        return new SimulationResult(
            new SimulationResultData
            {
                Seed = _options.Seed,
                Steps = _steps,
                VirtualElapsed = TimeProvider.GetElapsedVirtualTime(),
                Schedule = CreateSchedule(),
                Trace = trace,
                Invariants = invariantOutcomes,
                Consistency = consistencyOutcomes,
                Faults = Faults.AppliedFaults,
                Coverage = coverage,
                ExplorationMetadata = CreateExplorationMetadata(),
                TimeTravel = TimeTravel.CreateTimeline(),
                ProductionReality = ProductionReality.CreateEvidence()
            });
    }


    private ExplorationExecutionMetadata CreateExplorationMetadata()
    {
        return new ExplorationExecutionMetadata
        {
            Decisions = new List<ExplorationDecisionMetadata>(_explorationDecisions).AsReadOnly(),
            Operations = new Dictionary<long, ExplorationOperationRegistration>(_explorationOperations)
        };
    }


    private CoverageSnapshot CreateCoverageSnapshot()
    {
        foreach (var fault in Faults.AppliedFaults)
        {
            Coverage.RecordAutomatic($"fault:{fault.Scope}:{fault.PolicyName}");
        }

        return Coverage.CreateSnapshot();
    }

    private SimulationFailedException CreateFailure(Exception exception)
    {
        TimeTravel.CaptureAutomatic(TimeTravelCheckpointKind.Failure);
        var trace = new List<SimulationTraceEntry>(_trace).AsReadOnly();
        return new SimulationFailedException(
            new SimulationFailureData
            {
                Seed = _options.Seed,
                Schedule = CreateSchedule(),
                Trace = trace,
                Faults = Faults.AppliedFaults,
                TimeTravel = TimeTravel.CreateTimeline(),
                ProductionReality = ProductionReality.CreateEvidence()
            },
            exception);
    }

    private SimulationSchedule CreateSchedule()
    {
        return new SimulationSchedule(new List<ScheduleDecision>(_scheduleDecisions).AsReadOnly());
    }
}
