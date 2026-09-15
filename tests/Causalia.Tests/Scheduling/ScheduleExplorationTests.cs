using Causalia.Exceptions;
using Causalia.Scheduling;

namespace Causalia.Tests.Scheduling;

[TestClass]
public sealed class ScheduleExplorationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExploreAsync_WhenTwoContinuationsCanRun_ExploresBothSchedules()
    {
        var result = await Simulation.ExploreAsync(
            new ExplorationOptions
            {
                Simulation = new SimulationOptions { Seed = 1001 },
                MaxSchedules = 10,
                MaxDecisionDepth = 10
            },
            async context =>
            {
                await context.ConcurrentAsync(
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                    },
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.SchedulesExplored);
        Assert.IsTrue(result.ExhaustedWithinBounds);
        Assert.IsFalse(result.DepthLimitReached);
    }

    [TestMethod]
    public async Task ExploreAsync_WithThreeIndependentContinuations_ExploresAllSixOrders()
    {
        var result = await Simulation.ExploreAsync(
            new ExplorationOptions
            {
                Simulation = new SimulationOptions { Seed = 1005 },
                MaxSchedules = 20,
                MaxDecisionDepth = 10
            },
            async context =>
            {
                static async Task WorkAsync(CancellationToken cancellationToken)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var first = WorkAsync(context.CancellationToken);
                var second = WorkAsync(context.CancellationToken);
                var third = WorkAsync(context.CancellationToken);
                await Task.WhenAll(first, second, third);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(6, result.SchedulesExplored);
        Assert.IsTrue(result.ExhaustedWithinBounds);
    }

    [TestMethod]
    public async Task ExploreAsync_WhenOneScheduleExposesRace_ReturnsReplayableFailure()
    {
        var options = new SimulationOptions { Seed = 1002 };
        var exception = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Simulation = options,
                    MaxSchedules = 10,
                    MaxDecisionDepth = 10
                },
                RaceScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual(2, exception.SchedulesExplored);
        Assert.AreEqual(1, exception.Schedule.Decisions.Count);
        var parsedSchedule = SimulationSchedule.Parse(exception.Schedule.ReplayToken);

        var replayException = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReplayAsync(
                options,
                parsedSchedule,
                RaceScenarioAsync,
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<InvalidOperationException>(replayException.InnerException);
        Assert.AreEqual(exception.Schedule.ReplayToken, replayException.Schedule.ReplayToken);
    }

    [TestMethod]
    public async Task ExploreAsync_WhenScheduleLimitIsReached_ReportsIncompleteExploration()
    {
        var result = await Simulation.ExploreAsync(
            new ExplorationOptions
            {
                Simulation = new SimulationOptions { Seed = 1003 },
                MaxSchedules = 1,
                MaxDecisionDepth = 10
            },
            async context =>
            {
                await context.ConcurrentAsync(
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                    },
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.SchedulesExplored);
        Assert.IsFalse(result.ExhaustedWithinBounds);
    }

    [TestMethod]
    public async Task ReplayAsync_WhenExecutionShapeChanged_ThrowsReplayException()
    {
        var options = new SimulationOptions { Seed = 1004 };
        var original = await Simulation.RunAsync(
            options,
            async context =>
            {
                await context.ConcurrentAsync(
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                    },
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<SimulationScheduleReplayException>(async () =>
            await Simulation.ReplayAsync(
                options,
                original.Schedule,
                _ => Task.CompletedTask,
                TestContext.CancellationToken));
    }

    [DeterministicSimulation]
    private static async Task RaceScenarioAsync(SimulationContext context)
    {
        var value = 0;

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

                if (value == 0)
                {
                    throw new InvalidOperationException("The reader observed state before the writer ran.");
                }
            },
            context.CancellationToken);
    }
}
