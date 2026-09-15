using Causalia.ModelBased;
using Causalia.Scheduling;

namespace Causalia.Tests.ModelBased;

[TestClass]
public sealed class ModelBasedSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CheckModelAsync_WithDeterministicStateMachine_ExploresReachablePrefixes()
    {
        var result = await Simulation.CheckModelAsync(
            new ModelBasedOptions
            {
                Simulation = new SimulationOptions { Seed = 1601 },
                MaxSequences = 10,
                MaxCommandDepth = 5
            },
            CreateCounterSpecification(maximum: 2),
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.SequencesExplored);
        Assert.AreEqual(3L, result.CommandsExecuted);
        Assert.AreEqual(2, result.SchedulesExplored);
        Assert.AreEqual(2, result.MaximumDepthReached);
        Assert.IsTrue(result.ExhaustedWithinBounds);
        Assert.IsFalse(result.DepthLimitReached);
    }

    [TestMethod]
    public async Task CheckModelAsync_WhenVerifierRejectsObservation_ReturnsStructuredModelFailure()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationModelExplorationFailedException>(async () =>
            await Simulation.CheckModelAsync(
                new ModelBasedOptions
                {
                    Simulation = new SimulationOptions { Seed = 1602 },
                    MaxSequences = 10,
                    MaxCommandDepth = 5
                },
                CreateBrokenSpecification(),
                TestContext.CancellationToken));

        Assert.IsNotNull(failure.ModelFailure);
        Assert.AreEqual("broken-counter", failure.ModelFailure.ModelName);
        Assert.AreEqual("increment", failure.ModelFailure.CommandName);
        Assert.AreEqual(0, failure.ModelFailure.StepIndex);
        Assert.AreEqual(1, failure.ModelFailure.Sequence.Commands.Count);
        Assert.AreEqual(failure.ModelFailure, failure.Failure.ModelFailure);
    }

    [TestMethod]
    public async Task FailureSignature_DistinguishesDelimiterPlacementAcrossModelFields()
    {
        var first = await CaptureModelFailureSignatureAsync("a|b", "c", "d", 1611, TestContext.CancellationToken);
        var second = await CaptureModelFailureSignatureAsync("a", "b|c", "d", 1612, TestContext.CancellationToken);

        Assert.IsTrue(first.StartsWith("fi2:", StringComparison.Ordinal));
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public async Task ReplayModelAsync_WithPersistedSequence_ReplaysExactCommands()
    {
        var sequence = new ModelSequence(["increment", "increment"]);
        var result = await Simulation.ReplayModelAsync(
            new ModelBasedReplayOptions
            {
                Simulation = new SimulationOptions { Seed = 1603 }
            },
            sequence,
            CreateCounterSpecification(maximum: 2),
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.FinalState.Value);
        Assert.AreEqual(sequence.ReplayToken, result.Sequence.ReplayToken);
        Assert.IsTrue(result.Simulation.Trace.Any(entry => entry.Message == "model:counter:command:1:verified:increment"));
    }

    [TestMethod]
    public async Task ReplayModelAsync_WithModelAndSchedulerTokens_ReproducesExactFailure()
    {
        var options = new ModelBasedOptions
        {
            Simulation = new SimulationOptions { Seed = 1610 },
            MaxSequences = 10,
            MaxCommandDepth = 2,
            ScheduleExploration = new ModelBasedScheduleExplorationOptions
            {
                Strategy = ExplorationStrategy.DepthFirst,
                MaxSchedules = 20,
                MaxDecisionDepth = 20
            }
        };
        var specification = CreateScheduleSensitiveSpecification();
        var discovered = await Assert.ThrowsExactlyAsync<SimulationModelExplorationFailedException>(async () =>
            await Simulation.CheckModelAsync(options, specification, TestContext.CancellationToken));

        Assert.IsTrue(discovered.Schedule.Decisions.Count > 0);

        var replay = await Assert.ThrowsExactlyAsync<Causalia.Exceptions.SimulationFailedException>(async () =>
            await Simulation.ReplayModelAsync(
                new ModelBasedReplayOptions
                {
                    Simulation = options.Simulation,
                    Schedule = discovered.Schedule
                },
                discovered.Sequence,
                specification,
                TestContext.CancellationToken));

        Assert.AreEqual(discovered.Sequence.ReplayToken, replay.ModelFailure!.Sequence.ReplayToken);
        Assert.AreEqual(discovered.Schedule.ReplayToken, replay.Schedule.ReplayToken);
        Assert.IsInstanceOfType<SimulationModelViolationException>(replay.ModelFailure);
    }

    [TestMethod]
    public void ModelSequence_Parse_RoundTripsCommandNames()
    {
        var original = new ModelSequence(["add:item/42", "confirm with spaces", "✓"]);

        var parsed = ModelSequence.Parse(original.ReplayToken);

        CollectionAssert.AreEqual(original.Commands.ToList(), parsed.Commands.ToList());
        Assert.AreEqual(original.ReplayToken, parsed.ReplayToken);
    }

    [TestMethod]
    public async Task ReplayModelAsync_WhenCommandIsNoLongerEnabled_ThrowsReplayException()
    {
        var sequence = new ModelSequence(["increment", "increment", "increment"]);

        await Assert.ThrowsExactlyAsync<SimulationModelReplayException>(async () =>
            await Simulation.ReplayModelAsync(
                new ModelBasedReplayOptions
                {
                    Simulation = new SimulationOptions { Seed = 1604 }
                },
                sequence,
                CreateCounterSpecification(maximum: 2),
                TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CheckModelAsync_WhenDepthBoundStopsCycle_ReportsDepthLimit()
    {
        var result = await Simulation.CheckModelAsync(
            new ModelBasedOptions
            {
                Simulation = new SimulationOptions { Seed = 1605 },
                MaxSequences = 100,
                MaxCommandDepth = 3
            },
            CreateToggleSpecification(),
            TestContext.CancellationToken);

        Assert.AreEqual(3, result.SequencesExplored);
        Assert.AreEqual(3, result.MaximumDepthReached);
        Assert.IsTrue(result.ExhaustedWithinBounds);
        Assert.IsTrue(result.DepthLimitReached);
    }

    [TestMethod]
    public async Task CheckModelAsync_WithScheduleExploration_ComposesWithDpor()
    {
        var result = await Simulation.CheckModelAsync(
            new ModelBasedOptions
            {
                Simulation = new SimulationOptions { Seed = 1606 },
                MaxSequences = 10,
                MaxCommandDepth = 2,
                ScheduleExploration = new ModelBasedScheduleExplorationOptions
                {
                    Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
                    MaxSchedules = 100,
                    MaxDecisionDepth = 20
                }
            },
            CreateDporSpecification(),
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.SequencesExplored);
        Assert.IsTrue(result.SchedulesExplored >= 1);
        Assert.IsTrue(result.ExhaustedWithinBounds);
    }

    [TestMethod]
    public async Task MinimizeModelAsync_RemovesCommandsThatAreNotRequiredForFailure()
    {
        var sequence = new ModelSequence(["noop", "noop", "fail"]);
        var result = await Simulation.MinimizeModelAsync(
            new ModelBasedOptions
            {
                Simulation = new SimulationOptions { Seed = 1607 }
            },
            new ModelBasedMinimizationOptions
            {
                MaxAttempts = 50
            },
            sequence,
            CreateShrinkSpecification(),
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.MinimizedSequence.Commands.Count);
        Assert.AreEqual("fail", result.MinimizedSequence.Commands[0]);
        Assert.IsInstanceOfType<SimulationModelViolationException>(result.Failure.InnerException);
        Assert.IsTrue(result.Attempts > 0);
    }

    [TestMethod]
    public async Task CheckModelAsync_WhenNoCommandsAreEnabled_CompletesWithoutExecutingSimulation()
    {
        var specification = new ModelBasedSpecification<CounterState, CounterSystem>(
            "empty",
            () => new CounterState(0),
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CounterSystem());
            },
            static _ => []);

        var result = await Simulation.CheckModelAsync(
            new ModelBasedOptions
            {
                Simulation = new SimulationOptions { Seed = 1608 }
            },
            specification,
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.SequencesExplored);
        Assert.AreEqual(0, result.SchedulesExplored);
        Assert.IsTrue(result.ExhaustedWithinBounds);
    }

    [TestMethod]
    public async Task CheckModelAsync_UsesCommandPreconditionsToRestrictReachableTransitions()
    {
        var specification = new ModelBasedSpecification<CounterState, CounterSystem>(
            "preconditions",
            () => new CounterState(0),
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CounterSystem());
            },
            state =>
            [
                CreateIncrementCommand(maximum: 1),
                ModelCommand.Create<CounterState, CounterSystem, int>(
                    "decrement",
                    current => current with { Value = current.Value - 1 },
                    static (system, _, cancellationToken) => system.DecrementAsync(cancellationToken),
                    static (_, expected, observed) => ModelCommandVerification.Equal(expected.Value, observed),
                    current => current.Value > 0)
            ]);

        var result = await Simulation.CheckModelAsync(
            new ModelBasedOptions
            {
                Simulation = new SimulationOptions { Seed = 1609 },
                MaxSequences = 2,
                MaxCommandDepth = 3
            },
            specification,
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.SequencesExplored);
        Assert.AreEqual(2, result.MaximumDepthReached);
        Assert.IsFalse(result.ExhaustedWithinBounds);
    }

    private static ModelBasedSpecification<CounterState, CounterSystem> CreateCounterSpecification(int maximum)
    {
        return new ModelBasedSpecification<CounterState, CounterSystem>(
            "counter",
            () => new CounterState(0),
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CounterSystem());
            },
            state => [CreateIncrementCommand(maximum)]);
    }

    private static async Task<string> CaptureModelFailureSignatureAsync(
        string modelName,
        string commandName,
        string violationMessage,
        ulong seed,
        CancellationToken cancellationToken)
    {
        var specification = new ModelBasedSpecification<int, object>(
            modelName,
            static () => 0,
            static (_, operationCancellationToken) =>
            {
                operationCancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new object());
            },
            _ =>
            [
                ModelCommand.Create<int, object, int>(
                    commandName,
                    static state => state + 1,
                    static (_, _, operationCancellationToken) =>
                    {
                        operationCancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(1);
                    },
                    (_, _, _) => ModelCommandVerification.Fail(violationMessage))
            ]);
        var failure = await Assert.ThrowsExactlyAsync<SimulationModelExplorationFailedException>(async () =>
            await Simulation.CheckModelAsync(
                new ModelBasedOptions
                {
                    Simulation = new SimulationOptions { Seed = seed },
                    MaxSequences = 1,
                    MaxCommandDepth = 1
                },
                specification,
                cancellationToken));
        return failure.Failure.Signature.Token;
    }

    private static ModelBasedSpecification<CounterState, CounterSystem> CreateBrokenSpecification()
    {
        return new ModelBasedSpecification<CounterState, CounterSystem>(
            "broken-counter",
            () => new CounterState(0),
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CounterSystem(incrementBy: 2));
            },
            state => [CreateIncrementCommand(maximum: 1)]);
    }

    private static ModelCommand<CounterState, CounterSystem> CreateIncrementCommand(int maximum)
    {
        return ModelCommand.Create<CounterState, CounterSystem, int>(
            "increment",
            state => state with { Value = state.Value + 1 },
            static (system, _, cancellationToken) => system.IncrementAsync(cancellationToken),
            static (_, expected, observed) => ModelCommandVerification.Equal(expected.Value, observed),
            state => state.Value < maximum);
    }

    private static ModelBasedSpecification<ToggleState, ToggleSystem> CreateToggleSpecification()
    {
        return new ModelBasedSpecification<ToggleState, ToggleSystem>(
            "toggle",
            () => new ToggleState(false),
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new ToggleSystem());
            },
            _ =>
            [
                ModelCommand.Create<ToggleState, ToggleSystem, bool>(
                    "toggle",
                    state => state with { Value = !state.Value },
                    static (system, _, cancellationToken) => system.ToggleAsync(cancellationToken),
                    static (_, expected, observed) => ModelCommandVerification.Equal(expected.Value, observed))
            ]);
    }

    private static ModelBasedSpecification<int, object> CreateScheduleSensitiveSpecification()
    {
        return new ModelBasedSpecification<int, object>(
            "schedule-sensitive",
            static () => 0,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new object());
            },
            state =>
            [
                ModelCommand.Create<int, object, int>(
                    "race",
                    static _ => 1,
                    static async (_, context, cancellationToken) =>
                    {
                        var value = 0;
                        var observed = -1;
                        await context.ConcurrentAsync(
                            async operationCancellationToken =>
                            {
                                await Task.Yield();
                                operationCancellationToken.ThrowIfCancellationRequested();
                                value = 1;
                            },
                            async operationCancellationToken =>
                            {
                                await Task.Yield();
                                operationCancellationToken.ThrowIfCancellationRequested();
                                observed = value;
                            },
                            cancellationToken);
                        return observed;
                    },
                    static (_, expected, observed) => ModelCommandVerification.Equal(expected, observed),
                    current => current == 0)
            ]);
    }

    private static ModelBasedSpecification<int, object> CreateDporSpecification()
    {
        return new ModelBasedSpecification<int, object>(
            "dpor-model",
            static () => 0,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new object());
            },
            state =>
            [
                ModelCommand.Create<int, object, int>(
                    "parallel",
                    current => current + 1,
                    static async (_, context, cancellationToken) =>
                    {
                        var first = context.Exploration.RunAsync(
                            "model-a",
                            [context.Exploration.Write("model:a")],
                            async operationCancellationToken =>
                            {
                                await Task.Yield();
                                operationCancellationToken.ThrowIfCancellationRequested();
                            },
                            cancellationToken);
                        var second = context.Exploration.RunAsync(
                            "model-b",
                            [context.Exploration.Write("model:b")],
                            async operationCancellationToken =>
                            {
                                await Task.Yield();
                                operationCancellationToken.ThrowIfCancellationRequested();
                            },
                            cancellationToken);
                        await Task.WhenAll(first, second);
                        return 1;
                    },
                    static (_, expected, observed) => ModelCommandVerification.Equal(expected, observed),
                    current => current == 0)
            ]);
    }

    private static ModelBasedSpecification<int, object> CreateShrinkSpecification()
    {
        return new ModelBasedSpecification<int, object>(
            "shrink",
            static () => 0,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new object());
            },
            _ =>
            [
                ModelCommand.Create<int, object, int>(
                    "noop",
                    state => state,
                    static (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(0);
                    },
                    static (_, _, _) => ModelCommandVerification.Pass()),
                ModelCommand.Create<int, object, int>(
                    "fail",
                    state => state,
                    static (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(0);
                    },
                    static (_, _, _) => ModelCommandVerification.Fail("forced mismatch"))
            ]);
    }

    private sealed record CounterState(int Value);

    private sealed class CounterSystem
    {
        private readonly int _incrementBy;

        public CounterSystem(int incrementBy = 1)
        {
            _incrementBy = incrementBy;
        }

        public int Value { get; private set; }

        public Task<int> IncrementAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Value += _incrementBy;
            return Task.FromResult(Value);
        }

        public Task<int> DecrementAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Value--;
            return Task.FromResult(Value);
        }
    }

    private sealed record ToggleState(bool Value);

    private sealed class ToggleSystem
    {
        private bool _value;

        public Task<bool> ToggleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _value = !_value;
            return Task.FromResult(_value);
        }
    }
}
