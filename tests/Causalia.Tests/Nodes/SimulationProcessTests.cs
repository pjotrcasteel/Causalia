using Causalia.Processes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Tests.Nodes;

[TestClass]
public sealed class SimulationProcessTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RestartCreatesFreshGenerationAndCrashSkipsGracefulStop()
    {
        var created = new List<TestGeneration>();

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1201 },
            async context =>
            {
                await using var process = await context.StartProcessAsync(
                    new SimulationProcessOptions { Name = "worker" },
                    (generationContext, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var generation = new TestGeneration(generationContext.Generation);
                        created.Add(generation);
                        return Task.FromResult(generation);
                    },
                    TestContext.CancellationToken);

                var first = process.Current;
                Assert.AreEqual(1, first.Generation);
                Assert.AreEqual(1, first.StartCount);

                await process.CrashAsync(TestContext.CancellationToken);
                Assert.AreEqual(0, first.StopCount);
                Assert.AreEqual(1, first.DisposeCount);
                Assert.AreEqual(SimulationProcessState.Crashed, process.State);

                await process.RestartAsync(TestContext.CancellationToken);
                var second = process.Current;
                Assert.AreNotSame(first, second);
                Assert.AreEqual(2, second.Generation);
                Assert.AreEqual(1, second.StartCount);

                await process.StopAsync(TestContext.CancellationToken);
                Assert.AreEqual(1, second.StopCount);
                Assert.AreEqual(1, second.DisposeCount);
                Assert.AreEqual(SimulationProcessState.Stopped, process.State);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(2, created.Count);
    }

    [TestMethod]
    public async Task CrashCancelsGenerationBackgroundWorkAndRestartStartsItAgain()
    {
        var startedGenerations = new List<int>();
        var cancelledGenerations = new List<int>();

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1202 },
            async context =>
            {
                await using var process = await context.StartProcessAsync(
                    new SimulationProcessOptions { Name = "worker" },
                    (generationContext, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var generation = new TestGeneration(generationContext.Generation);
                        _ = generationContext.RunBackgroundAsync(
                            async generationCancellationToken =>
                            {
                                startedGenerations.Add(generationContext.Generation);

                                try
                                {
                                    await Task.Delay(
                                        TimeSpan.FromDays(1),
                                        context.TimeProvider,
                                        generationCancellationToken);
                                }
                                catch (OperationCanceledException) when (generationCancellationToken.IsCancellationRequested)
                                {
                                    cancelledGenerations.Add(generationContext.Generation);
                                }
                            });
                        return Task.FromResult(generation);
                    },
                    TestContext.CancellationToken);

                await process.CrashAsync(TestContext.CancellationToken);
                await process.RestartAsync(TestContext.CancellationToken);
                await process.StopAsync(TestContext.CancellationToken);
            },
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { 1, 2 }, startedGenerations);
        CollectionAssert.AreEqual(new[] { 1, 2 }, cancelledGenerations);
    }

    [TestMethod]
    public async Task StopAsync_WhenBackgroundWorkObservesGenerationCancellation_CompletesNormally()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1206 },
            async context =>
            {
                await using var process = await context.StartProcessAsync(
                    new SimulationProcessOptions { Name = "worker" },
                    (generationContext, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _ = generationContext.RunBackgroundAsync(
                            backgroundCancellationToken => Task.Delay(
                                TimeSpan.FromDays(1),
                                context.TimeProvider,
                                backgroundCancellationToken));
                        return Task.FromResult(new TestGeneration(generationContext.Generation));
                    },
                    context.CancellationToken);

                await process.StopAsync(context.CancellationToken);
                Assert.AreEqual(SimulationProcessState.Stopped, process.State);
            },
            TestContext.CancellationToken);
    }

    private sealed class TestGeneration : ISimulationProcessGeneration
    {
        public TestGeneration(int generation)
        {
            Generation = generation;
        }

        public int Generation { get; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
