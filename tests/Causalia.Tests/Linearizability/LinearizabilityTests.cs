using Causalia.Exceptions;
using Causalia.Linearizability;
using Causalia.Scheduling;

namespace Causalia.Tests.Linearizability;

[TestClass]
public sealed class LinearizabilityTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RequireLinearizable_WhenRegisterHistoryHasSequentialExplanation_ReturnsLinearization()
    {
        LinearizabilityResult? check = null;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1001 },
            context =>
            {
                var history = context.Linearizability.CreateHistory<RegisterInput, RegisterOutput>("register");
                var write = history.Begin("writer", RegisterInput.Write(200));
                var readBefore = history.Begin("reader-1", RegisterInput.Read());
                readBefore.Complete(RegisterOutput.FromValue(0));
                write.Complete(RegisterOutput.Ack());
                var readAfter = history.Begin("reader-2", RegisterInput.Read());
                readAfter.Complete(RegisterOutput.FromValue(200));
                check = history.RequireLinearizable(CreateRegisterSpecification());
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(check);
        Assert.IsTrue(check.IsLinearizable);
        Assert.AreEqual(3, check.Linearization.Count);
    }

    [TestMethod]
    public async Task RequireLinearizable_WhenNoSequentialExplanationExists_ExposesViolationOnSimulationFailure()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1002 },
                context =>
                {
                    var history = context.Linearizability.CreateHistory<RegisterInput, RegisterOutput>("register");
                    var write = history.Begin("writer", RegisterInput.Write(200));
                    var readOne = history.Begin("reader-1", RegisterInput.Read());
                    readOne.Complete(RegisterOutput.FromValue(200));
                    var readTwo = history.Begin("reader-2", RegisterInput.Read());
                    readTwo.Complete(RegisterOutput.FromValue(0));
                    write.Complete(RegisterOutput.Ack());
                    history.RequireLinearizable(CreateRegisterSpecification());
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationLinearizabilityViolationException>(failure.LinearizabilityFailure);
        Assert.AreEqual("register", failure.LinearizabilityFailure.HistoryName);
        Assert.AreEqual(LinearizabilityStatus.NotLinearizable, failure.LinearizabilityFailure.Result.Status);
    }

    [TestMethod]
    public async Task RequireLinearizable_WhenSearchBudgetIsExhausted_ReportsInconclusiveInsteadOfPassing()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1003 },
                context =>
                {
                    var history = context.Linearizability.CreateHistory<string, int>("bounded");
                    history.Begin("a", "noop").Complete(0);
                    history.Begin("b", "noop").Complete(0);
                    var specification = new LinearizabilitySpecification<int, string, int>(
                        0,
                        (state, _, _) => LinearizabilityStep<int>.Accept(state));
                    history.RequireLinearizable(specification, new LinearizabilityOptions { MaxSearchStates = 1 });
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationLinearizabilityInconclusiveException>(failure.LinearizabilityFailure);
        Assert.AreEqual(LinearizabilityStatus.Inconclusive, failure.LinearizabilityFailure.Result.Status);
    }

    [TestMethod]
    public async Task ExploreAsync_WhenLostUpdateCreatesInvalidHistory_FindsReplayableViolation()
    {
        var options = new SimulationOptions { Seed = 1004 };
        var exploration = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Simulation = options,
                    MaxSchedules = 100,
                    MaxDecisionDepth = 20
                },
                RunCounterScenarioAsync,
                TestContext.CancellationToken));

        Assert.IsNotNull(exploration.Failure.LinearizabilityFailure);

        var replay = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReplayAsync(
                options,
                exploration.Failure.Schedule,
                RunCounterScenarioAsync,
                TestContext.CancellationToken));

        Assert.IsNotNull(replay.LinearizabilityFailure);
        Assert.AreEqual("counter", replay.LinearizabilityFailure.HistoryName);
    }

    [DeterministicSimulation]
    private static async Task RunCounterScenarioAsync(SimulationContext context)
    {
        var value = 0;
        var history = context.Linearizability.CreateHistory<string, int>("counter");
        var specification = new LinearizabilitySpecification<int, string, int>(
            0,
            (state, input, output) => input == "increment" && output == state + 1
                ? LinearizabilityStep<int>.Accept(state + 1)
                : LinearizabilityStep<int>.Reject(state));

        await context.ConcurrentAsync(
            async cancellationToken =>
            {
                var operation = history.Begin("a", "increment");
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                var observed = value;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                value = observed + 1;
                operation.Complete(value);
            },
            async cancellationToken =>
            {
                var operation = history.Begin("b", "increment");
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                var observed = value;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                value = observed + 1;
                operation.Complete(value);
            },
            context.CancellationToken);

        history.RequireLinearizable(specification);
    }

    private static LinearizabilitySpecification<int, RegisterInput, RegisterOutput> CreateRegisterSpecification()
    {
        return new LinearizabilitySpecification<int, RegisterInput, RegisterOutput>(
            0,
            (state, input, output) => input.IsRead
                ? output.HasValue && output.Value == state
                    ? LinearizabilityStep<int>.Accept(state)
                    : LinearizabilityStep<int>.Reject(state)
                : !output.HasValue
                    ? LinearizabilityStep<int>.Accept(input.Value)
                    : LinearizabilityStep<int>.Reject(state));
    }

    private sealed record RegisterInput(bool IsRead, int Value)
    {
        public static RegisterInput Read() => new(true, 0);

        public static RegisterInput Write(int value) => new(false, value);
    }

    private sealed record RegisterOutput(bool HasValue, int Value)
    {
        public static RegisterOutput Ack() => new(false, 0);

        public static RegisterOutput FromValue(int value) => new(true, value);
    }
}
