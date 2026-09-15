using Causalia.Exceptions;
using Causalia.Faults;
using Causalia.Scheduling;

namespace Causalia.Tests.Scheduling;

[TestClass]
public sealed class CoverageGuidedExplorationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_WhenCoverageIsRecorded_ReturnsStableCoverageSnapshot()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 15001 },
            async context =>
            {
                context.Coverage.Hit("scenario-started");
                context.Coverage.Observe("phase", "initial");

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

        Assert.IsTrue(result.Coverage.Contains("user:hit:scenario-started"));
        Assert.IsTrue(result.Coverage.Contains("user:state:phase:initial"));
        Assert.IsTrue(result.Coverage.Points.Any(point => point.StartsWith("runtime:scheduler:branch:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_WhenFaultIsApplied_RecordsAutomaticFaultCoverage()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 15005 },
            context =>
            {
                var plan = new FaultPlan<int, string>()
                    .Add(new ProbabilityFaultPolicy<int, string>("always", 1, _ => "fault"));
                _ = context.CreateFaultInjector("coverage-fault", plan).Evaluate(1);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.Coverage.Contains("runtime:fault:coverage-fault:always"));
    }

    [TestMethod]
    public async Task ExploreAsync_WithCoverageGuidedStrategy_DiscoversDistinctObservedStates()
    {
        var result = await Simulation.ExploreAsync(
            new ExplorationOptions
            {
                Strategy = ExplorationStrategy.CoverageGuided,
                Simulation = new SimulationOptions { Seed = 15002 },
                MaxSchedules = 20,
                MaxDecisionDepth = 20
            },
            async context =>
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
                        context.Coverage.Observe("reader-value", value);
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(ExplorationStrategy.CoverageGuided, result.Strategy);
        Assert.AreEqual(2, result.SchedulesExplored);
        Assert.IsTrue(result.ExhaustedWithinBounds);
        Assert.IsTrue(result.CoveragePointsDiscovered >= 3);
        Assert.AreEqual(2, result.SchedulesWithNewCoverage);
    }

    [TestMethod]
    public async Task ExploreAsync_WithCoverageGuidance_StillExhaustsAllBoundedSchedules()
    {
        var result = await Simulation.ExploreAsync(
            new ExplorationOptions
            {
                Strategy = ExplorationStrategy.CoverageGuided,
                Simulation = new SimulationOptions { Seed = 15006 },
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
    public async Task ExploreAsync_WithCoverageGuidance_PrioritizesNovelBranchDescendants()
    {
        var depthFirst = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                CreateOptions(ExplorationStrategy.DepthFirst),
                GuidedFailureScenarioAsync,
                TestContext.CancellationToken));

        var guided = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                CreateOptions(ExplorationStrategy.CoverageGuided),
                GuidedFailureScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual(ExplorationStrategy.CoverageGuided, guided.Strategy);
        Assert.AreEqual(ExplorationStrategy.DepthFirst, depthFirst.Strategy);
        Assert.IsTrue(guided.SchedulesExplored < depthFirst.SchedulesExplored);
    }

    private static ExplorationOptions CreateOptions(ExplorationStrategy strategy)
    {
        return new ExplorationOptions
        {
            Strategy = strategy,
            Simulation = new SimulationOptions { Seed = 15003 },
            MaxSchedules = 100,
            MaxDecisionDepth = 20
        };
    }

    [DeterministicSimulation]
    private static async Task GuidedFailureScenarioAsync(SimulationContext context)
    {
        var gateOpen = false;

        async Task FirstAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            gateOpen = true;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }

        async Task NovelAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            context.Coverage.Observe("novel-gate-open", gateOpen);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            if (!gateOpen)
            {
                throw new InvalidOperationException("Novel branch resumed before the gate was opened.");
            }
        }

        async Task ThirdAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }

        var first = FirstAsync(context.CancellationToken);
        var novel = NovelAsync(context.CancellationToken);
        var third = ThirdAsync(context.CancellationToken);
        await Task.WhenAll(first, novel, third);
    }
}
