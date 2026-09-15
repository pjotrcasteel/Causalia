using Causalia.Exceptions;
using Causalia.Load.Profiles;
using Causalia.Load.Thresholds;
using Causalia.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Load.Tests;

[TestClass]
public sealed class SimulationLoadRunnerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FixedConcurrencyShouldUseVirtualTimeAndReturnLatencyMetrics()
    {
        LoadRunResult? loadResult = null;
        var simulation = await Simulation.RunAsync(
            new SimulationOptions { Seed = 17001 },
            async context =>
            {
                loadResult = await context.CreateLoadRunner().RunAsync(
                    new FixedConcurrencyLoadProfile
                    {
                        VirtualUsers = 10,
                        IterationsPerUser = 5
                    },
                    async iteration =>
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(100),
                            iteration.Simulation.TimeProvider,
                            iteration.CancellationToken);
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(loadResult);
        Assert.AreEqual(50L, loadResult.ScheduledIterations);
        Assert.AreEqual(50L, loadResult.CompletedIterations);
        Assert.AreEqual(0L, loadResult.DroppedIterations);
        Assert.AreEqual(10, loadResult.PeakConcurrency);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), loadResult.Latency.Median);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), loadResult.Latency.Percentile99);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), simulation.VirtualElapsed);
    }

    [TestMethod]
    public async Task ConstantArrivalRateShouldScheduleOpenModelArrivalsIndependentlyOfCompletion()
    {
        LoadRunResult? loadResult = null;
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 17002 },
            async context =>
            {
                loadResult = await context.CreateLoadRunner().RunAsync(
                    new ConstantArrivalRateLoadProfile
                    {
                        Rate = 10,
                        TimeUnit = TimeSpan.FromSeconds(1),
                        Duration = TimeSpan.FromSeconds(1),
                        MaxConcurrentIterations = 100
                    },
                    async iteration =>
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(50),
                            iteration.Simulation.TimeProvider,
                            iteration.CancellationToken);
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(loadResult);
        Assert.AreEqual(10L, loadResult.ScheduledIterations);
        Assert.AreEqual(10L, loadResult.StartedIterations);
        Assert.AreEqual(10L, loadResult.CompletedIterations);
        Assert.AreEqual(0L, loadResult.DroppedIterations);
        Assert.AreEqual(TimeSpan.FromSeconds(1), loadResult.VirtualElapsed);
    }

    [TestMethod]
    public async Task BurstShouldDeterministicallyDropIterationsAboveConcurrencyCapacity()
    {
        LoadRunResult? loadResult = null;
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 17003 },
            async context =>
            {
                loadResult = await context.CreateLoadRunner().RunAsync(
                    new BurstLoadProfile
                    {
                        Iterations = 10,
                        MaxConcurrentIterations = 3
                    },
                    async iteration =>
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(1),
                            iteration.Simulation.TimeProvider,
                            iteration.CancellationToken);
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(loadResult);
        Assert.AreEqual(10L, loadResult.ScheduledIterations);
        Assert.AreEqual(3L, loadResult.StartedIterations);
        Assert.AreEqual(7L, loadResult.DroppedIterations);
        Assert.AreEqual(3, loadResult.PeakConcurrency);
    }

    [TestMethod]
    public async Task RampingArrivalRateShouldIntegrateLinearRateStagesDeterministically()
    {
        LoadRunResult? loadResult = null;
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 17004 },
            async context =>
            {
                loadResult = await context.CreateLoadRunner().RunAsync(
                    new RampingArrivalRateLoadProfile
                    {
                        StartRate = 0,
                        TimeUnit = TimeSpan.FromSeconds(1),
                        MaxConcurrentIterations = 100,
                        Stages = new List<ArrivalRateStage>
                        {
                            new() { Duration = TimeSpan.FromSeconds(1), TargetRate = 10 },
                            new() { Duration = TimeSpan.FromSeconds(1), TargetRate = 10 }
                        }
                    },
                    async iteration =>
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(10),
                            iteration.Simulation.TimeProvider,
                            iteration.CancellationToken);
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(loadResult);
        Assert.AreEqual(15L, loadResult.ScheduledIterations);
        Assert.AreEqual(15L, loadResult.CompletedIterations);
        Assert.AreEqual(0L, loadResult.DroppedIterations);
    }

    [TestMethod]
    public async Task ThresholdShouldFailSimulationWithCompletedLoadMetrics()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 17005 },
                async context =>
                {
                    await context.CreateLoadRunner().RunAsync(
                        new BurstLoadProfile
                        {
                            Iterations = 10,
                            MaxConcurrentIterations = 10
                        },
                        iteration =>
                        {
                            if (iteration.IterationId % 2 == 0)
                            {
                                throw new InvalidOperationException("synthetic failure");
                            }

                            return Task.CompletedTask;
                        },
                        new LoadRunOptions
                        {
                            FailureMode = LoadIterationFailureMode.RecordAndContinue,
                            Thresholds = new LoadThresholds
                            {
                                MaximumFailureRate = 0.40
                            }
                        },
                        context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationLoadThresholdException>(failure.InnerException);
        var threshold = (SimulationLoadThresholdException)failure.InnerException!;
        Assert.AreEqual(5L, threshold.Result.FailedIterations);
        Assert.AreEqual(0.5d, threshold.Result.FailureRate);
        Assert.AreEqual(1, threshold.Violations.Count);
        Assert.AreEqual("maximum-failure-rate", threshold.Violations[0].Threshold);
    }

    [TestMethod]
    public async Task ExplorationShouldDiscoverAndExactlyReplayRaceUnderBurstLoad()
    {
        var explorationFailure = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Strategy = ExplorationStrategy.CoverageGuided,
                    Simulation = new SimulationOptions { Seed = 17006 },
                    MaxSchedules = 100,
                    MaxDecisionDepth = 50
                },
                LoadRaceScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual("lost-update-under-load", explorationFailure.Failure.InnerException?.Message);

        var replayFailure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReplayAsync(
                new SimulationOptions { Seed = 17006 },
                explorationFailure.Failure.Schedule,
                LoadRaceScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual("lost-update-under-load", replayFailure.InnerException?.Message);
    }


    [TestMethod]
    public async Task StopOnFirstFailureShouldStopSchedulingNewOpenModelArrivals()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 17007 },
                async context =>
                {
                    await context.CreateLoadRunner().RunAsync(
                        new ConstantArrivalRateLoadProfile
                        {
                            Rate = 10,
                            TimeUnit = TimeSpan.FromSeconds(1),
                            Duration = TimeSpan.FromSeconds(1),
                            MaxConcurrentIterations = 10
                        },
                        _ => throw new InvalidOperationException("first-load-failure"),
                        new LoadRunOptions { TraceIterations = true },
                        context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.AreEqual("first-load-failure", failure.InnerException?.Message);
        Assert.AreEqual(1, failure.Trace.Count(entry => entry.Message.StartsWith("load:iteration:started:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_WhenProfileExceedsMaximumIterations_RejectsBeforeScheduling()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 17008 },
                async context =>
                {
                    await context.CreateLoadRunner().RunAsync(
                        new BurstLoadProfile
                        {
                            Iterations = 11,
                            MaxConcurrentIterations = 11
                        },
                        iteration => Task.Delay(
                            TimeSpan.FromSeconds(1),
                            iteration.Simulation.TimeProvider,
                            iteration.CancellationToken),
                        new LoadRunOptions { MaximumIterations = 10 },
                        context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<ArgumentOutOfRangeException>(failure.InnerException);
        var exception = (ArgumentOutOfRangeException)failure.InnerException!;
        Assert.AreEqual("options", exception.ParamName);
        Assert.AreEqual(11L, exception.ActualValue);
    }

    private static async Task LoadRaceScenarioAsync(SimulationContext context)
    {
        var value = 0;
        await context.CreateLoadRunner().RunAsync(
            new BurstLoadProfile
            {
                Iterations = 2,
                MaxConcurrentIterations = 2
            },
            async iteration =>
            {
                var observed = value;
                await Task.Yield();
                iteration.CancellationToken.ThrowIfCancellationRequested();
                value = observed + 1;
            },
            context.CancellationToken);

        if (value != 2)
        {
            throw new InvalidOperationException("lost-update-under-load");
        }
    }
}
