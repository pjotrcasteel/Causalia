using Causalia.Exceptions;
using Causalia.Scheduling;

namespace Causalia.Tests.Scheduling;

[TestClass]
public sealed class DporExplorationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExploreAsync_WithIndependentOperations_PrunesEquivalentSchedules()
    {
        var result = await Simulation.ExploreAsync(
            CreateOptions(),
            RunIndependentScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.SchedulesExplored);
        Assert.IsTrue(result.EquivalentSchedulesPruned > 0);
        Assert.IsTrue(result.ExhaustedWithinBounds);
    }

    [TestMethod]
    public async Task ExploreAsync_WithReadOnlySharedResource_PrunesEquivalentSchedules()
    {
        var result = await Simulation.ExploreAsync(
            CreateOptions(),
            RunReadOnlyScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.SchedulesExplored);
        Assert.IsTrue(result.EquivalentSchedulesPruned > 0);
        Assert.IsTrue(result.ExhaustedWithinBounds);
    }

    [TestMethod]
    public async Task ExploreAsync_WithReadWriteConflict_PreservesBothOrders()
    {
        var result = await Simulation.ExploreAsync(
            CreateOptions(),
            RunReadWriteScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.SchedulesExplored);
        Assert.AreEqual(0, result.EquivalentSchedulesPruned);
    }

    [TestMethod]
    public async Task ExploreAsync_WithIndependentEqualTimers_PreservesOperationLineage()
    {
        var result = await Simulation.ExploreAsync(
            CreateOptions(),
            RunIndependentTimerScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.SchedulesExplored);
        Assert.IsTrue(result.EquivalentSchedulesPruned > 0);
    }

    [TestMethod]
    public async Task ExploreAsync_WithConflictingOperations_PreservesAllOrders()
    {
        var result = await Simulation.ExploreAsync(
            CreateOptions(),
            RunConflictingScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(6, result.SchedulesExplored);
        Assert.AreEqual(0, result.EquivalentSchedulesPruned);
    }

    [TestMethod]
    public async Task ExploreAsync_WithUnmodeledOperations_RemainsConservative()
    {
        var result = await Simulation.ExploreAsync(
            CreateOptions(),
            async context =>
            {
                static async Task WorkAsync(CancellationToken cancellationToken)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await Task.WhenAll(
                    WorkAsync(context.CancellationToken),
                    WorkAsync(context.CancellationToken),
                    WorkAsync(context.CancellationToken));
            },
            TestContext.CancellationToken);

        Assert.AreEqual(6, result.SchedulesExplored);
    }

    [TestMethod]
    public async Task ExploreAsync_WithPreemptionBound_ReportsPrunedAlternatives()
    {
        var result = await Simulation.ExploreAsync(
            new ExplorationOptions
            {
                Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
                Simulation = new SimulationOptions { Seed = 1404 },
                MaxSchedules = 100,
                MaxDecisionDepth = 20,
                MaxPreemptions = 0
            },
            RunPreemptibleConflictingScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(0, result.MaximumPreemptionsObserved);
        Assert.IsTrue(result.PreemptionBoundPruned > 0);
    }

    [TestMethod]
    public async Task ReplayAsync_WithDporFailure_ReproducesExactSchedule()
    {
        var options = new SimulationOptions { Seed = 1405 };
        var failure = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
                    Simulation = options,
                    MaxSchedules = 100,
                    MaxDecisionDepth = 20
                },
                RunRaceScenarioAsync,
                TestContext.CancellationToken));

        var replay = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReplayAsync(
                options,
                failure.Schedule,
                RunRaceScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual(failure.Schedule.ReplayToken, replay.Schedule.ReplayToken);
    }

    private static ExplorationOptions CreateOptions()
    {
        return new ExplorationOptions
        {
            Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
            Simulation = new SimulationOptions { Seed = 1401 },
            MaxSchedules = 100,
            MaxDecisionDepth = 20
        };
    }

    [DeterministicSimulation]
    private static async Task RunIndependentScenarioAsync(SimulationContext context)
    {
        await Task.WhenAll(
            StartOperationAsync(context, "a", "a"),
            StartOperationAsync(context, "b", "b"),
            StartOperationAsync(context, "c", "c"));
    }

    [DeterministicSimulation]
    private static async Task RunReadOnlyScenarioAsync(SimulationContext context)
    {
        await Task.WhenAll(
            StartReadOperationAsync(context, "a"),
            StartReadOperationAsync(context, "b"),
            StartReadOperationAsync(context, "c"));
    }

    [DeterministicSimulation]
    private static async Task RunReadWriteScenarioAsync(SimulationContext context)
    {
        await Task.WhenAll(
            StartReadOperationAsync(context, "reader"),
            StartOperationAsync(context, "writer", "shared"));
    }

    [DeterministicSimulation]
    private static async Task RunIndependentTimerScenarioAsync(SimulationContext context)
    {
        await Task.WhenAll(
            StartTimedOperationAsync(context, "a", "a"),
            StartTimedOperationAsync(context, "b", "b"),
            StartTimedOperationAsync(context, "c", "c"));
    }

    [DeterministicSimulation]
    private static async Task RunConflictingScenarioAsync(SimulationContext context)
    {
        await Task.WhenAll(
            StartOperationAsync(context, "a", "shared"),
            StartOperationAsync(context, "b", "shared"),
            StartOperationAsync(context, "c", "shared"));
    }

    [DeterministicSimulation]
    private static async Task RunPreemptibleConflictingScenarioAsync(SimulationContext context)
    {
        await Task.WhenAll(
            StartPreemptibleOperationAsync(context, "a"),
            StartPreemptibleOperationAsync(context, "b"),
            StartPreemptibleOperationAsync(context, "c"));
    }

    private static Task StartReadOperationAsync(SimulationContext context, string name)
    {
        return context.Exploration.RunAsync(
            name,
            [context.Exploration.Read("shared")],
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            },
            context.CancellationToken);
    }

    private static Task StartTimedOperationAsync(SimulationContext context, string name, string resource)
    {
        return context.Exploration.RunAsync(
            name,
            [context.Exploration.Write(resource)],
            async cancellationToken =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            },
            context.CancellationToken);
    }

    private static Task StartOperationAsync(SimulationContext context, string name, string resource)
    {
        return context.Exploration.RunAsync(
            name,
            [context.Exploration.Write(resource)],
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            },
            context.CancellationToken);
    }

    private static Task StartPreemptibleOperationAsync(SimulationContext context, string name)
    {
        return context.Exploration.RunAsync(
            name,
            [context.Exploration.Write("shared")],
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            },
            context.CancellationToken);
    }

    [DeterministicSimulation]
    private static async Task RunRaceScenarioAsync(SimulationContext context)
    {
        var value = 0;
        var first = context.Exploration.RunAsync(
            "first",
            [context.Exploration.Write("counter")],
            async cancellationToken =>
            {
                var current = value;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                value = current + 1;
            },
            context.CancellationToken);
        var second = context.Exploration.RunAsync(
            "second",
            [context.Exploration.Write("counter")],
            async cancellationToken =>
            {
                var current = value;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                value = current + 1;
            },
            context.CancellationToken);
        await Task.WhenAll(first, second);

        if (value != 2)
        {
            throw new InvalidOperationException("Lost update.");
        }
    }
}
