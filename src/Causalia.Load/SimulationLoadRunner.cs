using Causalia.Load.Profiles;
using Causalia.Load.Thresholds;

namespace Causalia.Load;

/// <summary>
/// Executes deterministic closed-model and open-model workloads inside a Causalia simulation.
/// </summary>
public sealed class SimulationLoadRunner
{
    private readonly SimulationContext _context;
    private long _nextIterationId;

    internal SimulationLoadRunner(SimulationContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Executes the supplied deterministic load profile and returns virtual load metrics.
    /// </summary>
    public async Task<LoadRunResult> RunAsync(
        LoadProfile profile,
        Func<LoadIterationContext, Task> iteration,
        LoadRunOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(iteration);
        options ??= new LoadRunOptions();
        Validate(profile, options);
        ValidatePlannedIterations(profile, options);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAt = _context.TimeProvider.GetTimestamp();
        var metrics = new LoadMetricsAccumulator(options.MaximumFailureSamples);
        var state = new LoadExecutionState
        {
            Iteration = iteration,
            Options = options,
            Metrics = metrics,
            CancellationToken = cancellationToken
        };
        _context.TraceEvent($"load:run:started:{profile.Name}");
        _context.Coverage.Hit($"load:{profile.Name}:started");

        switch (profile)
        {
            case FixedConcurrencyLoadProfile fixedConcurrency:
                await RunFixedConcurrencyAsync(fixedConcurrency, state);
                break;

            case ConstantArrivalRateLoadProfile arrivalRate:
                await RunConstantArrivalRateAsync(arrivalRate, state);
                break;

            case BurstLoadProfile burst:
                await RunBurstAsync(burst, state);
                break;

            case RampingArrivalRateLoadProfile ramping:
                await RunRampingArrivalRateAsync(ramping, state);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile.GetType().FullName, "Unsupported load profile type.");
        }

        var elapsed = _context.TimeProvider.GetElapsedTime(startedAt);
        var result = CreateResult(profile.Name, elapsed, metrics);
        RecordCompletion(result);
        var violations = LoadThresholdEvaluator.Evaluate(result, options.Thresholds);

        if (violations.Count > 0)
        {
            throw new SimulationLoadThresholdException(result, violations);
        }

        return result;
    }

    /// <summary>
    /// Executes the supplied deterministic load profile using default run options.
    /// </summary>
    public Task<LoadRunResult> RunAsync(
        LoadProfile profile,
        Func<LoadIterationContext, Task> iteration,
        CancellationToken cancellationToken)
    {
        return RunAsync(profile, iteration, null, cancellationToken);
    }

    private async Task RunFixedConcurrencyAsync(FixedConcurrencyLoadProfile profile, LoadExecutionState state)
    {
        var actors = new List<Task>(profile.VirtualUsers);

        for (var actorId = 1; actorId <= profile.VirtualUsers; actorId++)
        {
            actors.Add(RunClosedActorAsync(actorId, profile, state));
        }

        await Task.WhenAll(actors);
    }

    private async Task RunClosedActorAsync(int actorId, FixedConcurrencyLoadProfile profile, LoadExecutionState state)
    {
        for (var index = 0; index < profile.IterationsPerUser; index++)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            state.ThrowIfFatalFailure();
            state.Metrics.Scheduled();
            var iterationId = checked(++_nextIterationId);
            var scheduledAt = _context.TimeProvider.GetUtcNow();
            await ExecuteIterationAsync(iterationId, actorId, scheduledAt, state);

            if (index + 1 < profile.IterationsPerUser && profile.ThinkTime > TimeSpan.Zero)
            {
                await Task.Delay(profile.ThinkTime, _context.TimeProvider, state.CancellationToken);
            }
            else if (index + 1 < profile.IterationsPerUser)
            {
                await Task.Yield();
            }
        }
    }

    private async Task RunConstantArrivalRateAsync(ConstantArrivalRateLoadProfile profile, LoadExecutionState state)
    {
        var actorPool = new OpenLoadActorPool(profile.MaxConcurrentIterations);
        var operations = new List<Task>();
        var origin = _context.TimeProvider.GetUtcNow();
        var count = CalculateArrivalCount(profile);

        for (long index = 0; index < count; index++)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            state.ThrowIfFatalFailure();
            var offset = CalculateArrivalOffset(profile, index);
            await DelayUntilAsync(origin + offset, state.CancellationToken);
            state.ThrowIfFatalFailure();
            state.Metrics.Scheduled();
            var iterationId = checked(++_nextIterationId);

            if (!actorPool.TryAcquire(out var actorId))
            {
                state.Metrics.Dropped();
                TraceDropped(iterationId, state.Options);
                continue;
            }

            var operation = ExecuteOpenIterationAsync(
                iterationId,
                actorId,
                origin + offset,
                actorPool,
                state);
            operations.Add(operation);

            if ((index & 0xFF) == 0xFF)
            {
                RemoveCompletedOperations(operations);
            }
        }

        await Task.WhenAll(operations);
    }


    private async Task RunRampingArrivalRateAsync(RampingArrivalRateLoadProfile profile, LoadExecutionState state)
    {
        var actorPool = new OpenLoadActorPool(profile.MaxConcurrentIterations);
        var operations = new List<Task>();
        var origin = _context.TimeProvider.GetUtcNow();
        var offsets = RampingArrivalSchedule.Create(profile, state.Options.MaximumIterations);
        var scheduledIndex = 0;

        foreach (var offset in offsets)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            state.ThrowIfFatalFailure();
            await DelayUntilAsync(origin + offset, state.CancellationToken);
            state.ThrowIfFatalFailure();
            state.Metrics.Scheduled();
            var iterationId = checked(++_nextIterationId);

            if (!actorPool.TryAcquire(out var actorId))
            {
                state.Metrics.Dropped();
                TraceDropped(iterationId, state.Options);
                continue;
            }

            operations.Add(
                ExecuteOpenIterationAsync(
                    iterationId,
                    actorId,
                    origin + offset,
                    actorPool,
                    state));
            scheduledIndex++;

            if ((scheduledIndex & 0xFF) == 0)
            {
                RemoveCompletedOperations(operations);
            }
        }

        await Task.WhenAll(operations);
    }

    private async Task RunBurstAsync(BurstLoadProfile profile, LoadExecutionState state)
    {
        var actorPool = new OpenLoadActorPool(profile.MaxConcurrentIterations);
        var operations = new List<Task>(Math.Min(profile.Iterations, profile.MaxConcurrentIterations));
        var scheduledAt = _context.TimeProvider.GetUtcNow();

        for (var index = 0; index < profile.Iterations; index++)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            state.ThrowIfFatalFailure();
            state.Metrics.Scheduled();
            var iterationId = checked(++_nextIterationId);

            if (!actorPool.TryAcquire(out var actorId))
            {
                state.Metrics.Dropped();
                TraceDropped(iterationId, state.Options);
                continue;
            }

            operations.Add(
                ExecuteOpenIterationAsync(
                    iterationId,
                    actorId,
                    scheduledAt,
                    actorPool,
                    state));
        }

        await Task.WhenAll(operations);
    }

    private async Task ExecuteOpenIterationAsync(
        long iterationId,
        int actorId,
        DateTimeOffset scheduledAt,
        OpenLoadActorPool actorPool,
        LoadExecutionState state)
    {
        try
        {
            await ExecuteIterationAsync(iterationId, actorId, scheduledAt, state);
        }
        finally
        {
            actorPool.Release(actorId);
        }
    }

    private async Task ExecuteIterationAsync(
        long iterationId,
        int actorId,
        DateTimeOffset scheduledAt,
        LoadExecutionState state)
    {
        state.CancellationToken.ThrowIfCancellationRequested();
        var startedAt = _context.TimeProvider.GetTimestamp();
        state.Metrics.Started();
        RecordPeakCoverage(state.Metrics.PeakConcurrency);
        TraceIteration("started", iterationId, actorId, state.Options);
        await Task.Yield();

        try
        {
            var iterationContext = new LoadIterationContext(_context, iterationId, actorId, scheduledAt, state.CancellationToken);
            await state.Iteration(iterationContext);
            var durationTicks = _context.TimeProvider.GetElapsedTime(startedAt).Ticks;
            state.Metrics.Completed(durationTicks);
            TraceIteration("completed", iterationId, actorId, state.Options);
        }
        catch (Exception exception) when (state.Options.FailureMode == LoadIterationFailureMode.RecordAndContinue &&
                                          exception is not OperationCanceledException)
        {
            var durationTicks = _context.TimeProvider.GetElapsedTime(startedAt).Ticks;
            state.Metrics.Failed(iterationId, actorId, durationTicks, exception);
            _context.Coverage.Hit("load:iteration-failed");
            TraceIteration("failed", iterationId, actorId, state.Options);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var durationTicks = _context.TimeProvider.GetElapsedTime(startedAt).Ticks;
            state.Metrics.Failed(iterationId, actorId, durationTicks, exception);
            state.CaptureFatalFailure(exception);
            _context.Coverage.Hit("load:iteration-failed");
            TraceIteration("failed", iterationId, actorId, state.Options);
            throw;
        }
    }

    private static void RemoveCompletedOperations(List<Task> operations)
    {
        for (var index = operations.Count - 1; index >= 0; index--)
        {
            if (operations[index].IsCompletedSuccessfully)
            {
                operations.RemoveAt(index);
            }
        }
    }

    private async Task DelayUntilAsync(DateTimeOffset target, CancellationToken cancellationToken)
    {
        var delay = target - _context.TimeProvider.GetUtcNow();

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, _context.TimeProvider, cancellationToken);
        }
    }

    private void RecordPeakCoverage(int peakConcurrency)
    {
        _context.Coverage.Observe("load:peak-concurrency", peakConcurrency);
    }

    private void TraceDropped(long iterationId, LoadRunOptions options)
    {
        _context.Coverage.Hit("load:iteration-dropped");

        if (options.TraceIterations)
        {
            _context.TraceEvent($"load:iteration:dropped:{iterationId}");
        }
    }

    private void TraceIteration(string state, long iterationId, int actorId, LoadRunOptions options)
    {
        if (options.TraceIterations)
        {
            _context.TraceEvent($"load:iteration:{state}:{iterationId}:actor:{actorId}");
        }
    }

    private void RecordCompletion(LoadRunResult result)
    {
        _context.Coverage.Observe("load:completed", result.CompletedIterations);
        _context.Coverage.Observe("load:failed", result.FailedIterations);
        _context.Coverage.Observe("load:dropped", result.DroppedIterations);
        _context.TraceEvent(
            $"load:run:completed:{result.ProfileName}:scheduled:{result.ScheduledIterations}:started:{result.StartedIterations}:" +
            $"completed:{result.CompletedIterations}:failed:{result.FailedIterations}:dropped:{result.DroppedIterations}:" +
            $"peak:{result.PeakConcurrency}");
    }

    private static LoadRunResult CreateResult(string profileName, TimeSpan elapsed, LoadMetricsAccumulator metrics)
    {
        return new LoadRunResult(
            new LoadRunResultData
            {
                ProfileName = profileName,
                ScheduledIterations = metrics.ScheduledIterations,
                StartedIterations = metrics.StartedIterations,
                CompletedIterations = metrics.CompletedIterations,
                FailedIterations = metrics.FailedIterations,
                DroppedIterations = metrics.DroppedIterations,
                PeakConcurrency = metrics.PeakConcurrency,
                VirtualElapsed = elapsed,
                Latency = metrics.CreateLatencyStatistics(),
                Failures = metrics.Failures
            });
    }

    private static long CalculateArrivalCount(ConstantArrivalRateLoadProfile profile)
    {
        var expected = (decimal)profile.Duration.Ticks * profile.Rate / profile.TimeUnit.Ticks;
        var count = decimal.Ceiling(expected - 0.5m);
        return count <= 0m ? 0L : decimal.ToInt64(count);
    }

    private static TimeSpan CalculateArrivalOffset(ConstantArrivalRateLoadProfile profile, long index)
    {
        var ticks = decimal.Floor(((decimal)index + 0.5m) * profile.TimeUnit.Ticks / profile.Rate);
        return TimeSpan.FromTicks(decimal.ToInt64(ticks));
    }

    private static void Validate(LoadProfile profile, LoadRunOptions options)
    {
        profile.Validate();
        options.Thresholds?.Validate();

        if (options.MaximumFailureSamples < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaximumFailureSamples,
                "MaximumFailureSamples cannot be negative.");
        }

        if (options.MaximumIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaximumIterations,
                "MaximumIterations must be greater than zero.");
        }
    }

    private static void ValidatePlannedIterations(LoadProfile profile, LoadRunOptions options)
    {
        var plannedIterations = profile switch
        {
            FixedConcurrencyLoadProfile fixedConcurrency =>
                checked((long)fixedConcurrency.VirtualUsers * fixedConcurrency.IterationsPerUser),
            ConstantArrivalRateLoadProfile arrivalRate => CalculateArrivalCount(arrivalRate),
            BurstLoadProfile burst => burst.Iterations,
            RampingArrivalRateLoadProfile => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.GetType().FullName, "Unsupported load profile type.")
        };

        if (plannedIterations > options.MaximumIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                plannedIterations,
                $"The profile schedules {plannedIterations} iterations, exceeding MaximumIterations {options.MaximumIterations}.");
        }
    }
}
