using Causalia.Exceptions;
using Causalia.Invariants;
using Causalia.Scheduling;

namespace Causalia.Tests.Invariants;

[TestClass]
public sealed class SimulationInvariantTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Always_WhenConditionBecomesFalse_FailsWithInvariantDetails()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 801 },
                async context =>
                {
                    var value = 1;
                    context.Invariants.Always("value remains positive", () => value > 0);
                    await Task.Yield();
                    value = 0;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantViolationException>(exception.InnerException);
        var invariantException = (SimulationInvariantViolationException)exception.InnerException!;
        Assert.AreEqual("value remains positive", invariantException.InvariantName);
        Assert.AreEqual(InvariantKind.Always, invariantException.Kind);
        Assert.IsNull(invariantException.Deadline);
    }

    [TestMethod]
    public async Task Never_WhenForbiddenStateIsObserved_Fails()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 802 },
                async context =>
                {
                    var forbidden = false;
                    context.Invariants.Never("forbidden state", () => forbidden);
                    await Task.Yield();
                    forbidden = true;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantViolationException>(exception.InnerException);
        var invariantException = (SimulationInvariantViolationException)exception.InnerException!;
        Assert.AreEqual(InvariantKind.Never, invariantException.Kind);
    }

    [TestMethod]
    public async Task Eventually_WhenConditionBecomesTrueAtDeadline_CompletesSuccessfully()
    {
        var startTime = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var result = await Simulation.RunAsync(
            new SimulationOptions
            {
                Seed = 803,
                StartTime = startTime
            },
            async context =>
            {
                var ready = false;
                context.Invariants.Eventually("worker becomes ready", TimeSpan.FromSeconds(5), () => ready);
                await Task.Delay(TimeSpan.FromSeconds(5), context.TimeProvider, context.CancellationToken);
                ready = true;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromSeconds(5), result.VirtualElapsed);
        Assert.AreEqual(1, result.Invariants.Count);
        var outcome = result.Invariants[0];
        Assert.AreEqual(InvariantKind.Eventually, outcome.Kind);
        Assert.AreEqual(startTime.AddSeconds(5), outcome.CompletedAt);
    }

    [TestMethod]
    public async Task Eventually_WhenDeadlineExpires_FailsAtVirtualDeadline()
    {
        var startTime = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions
                {
                    Seed = 804,
                    StartTime = startTime
                },
                context =>
                {
                    context.Invariants.Eventually("replica converges", TimeSpan.FromSeconds(3), () => false);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantViolationException>(exception.InnerException);
        var invariantException = (SimulationInvariantViolationException)exception.InnerException!;
        Assert.AreEqual(InvariantKind.Eventually, invariantException.Kind);
        Assert.AreEqual(startTime.AddSeconds(3), invariantException.Deadline);
        Assert.AreEqual(startTime.AddSeconds(3), invariantException.ObservedAt);
    }

    [TestMethod]
    public async Task InvariantPredicate_WhenItThrows_PreservesInnerException()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 805 },
                context =>
                {
                    context.Invariants.Always("query remains valid", () => throw new InvalidOperationException("predicate failure"));
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantEvaluationException>(exception.InnerException);
        var invariantException = (SimulationInvariantEvaluationException)exception.InnerException!;
        Assert.IsInstanceOfType<InvalidOperationException>(invariantException.InnerException);
        Assert.AreEqual("query remains valid", invariantException.InvariantName);
    }

    [TestMethod]
    public async Task ExploreAsync_WhenOnlyOneScheduleViolatesInvariant_ReturnsReplayableFailure()
    {
        var options = new SimulationOptions { Seed = 806 };
        var exception = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Simulation = options,
                    MaxSchedules = 10,
                    MaxDecisionDepth = 10
                },
                RaceInvariantScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual(2, exception.SchedulesExplored);
        Assert.IsInstanceOfType<SimulationFailedException>(exception.InnerException);
        var simulationFailure = (SimulationFailedException)exception.InnerException!;
        Assert.IsInstanceOfType<SimulationInvariantViolationException>(simulationFailure.InnerException);

        var replayException = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReplayAsync(
                options,
                SimulationSchedule.Parse(exception.Schedule.ReplayToken),
                RaceInvariantScenarioAsync,
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantViolationException>(replayException.InnerException);
        Assert.AreEqual(exception.Schedule.ReplayToken, replayException.Schedule.ReplayToken);
    }

    [DeterministicSimulation]
    private static async Task RaceInvariantScenarioAsync(SimulationContext context)
    {
        var value = 0;
        var staleReadObserved = false;
        context.Invariants.Never("reader observes stale value", () => staleReadObserved);

        await context.ConcurrentAsync(
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                value = 1;
            },
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                staleReadObserved = value == 0;
            },
            context.CancellationToken);
    }
}
