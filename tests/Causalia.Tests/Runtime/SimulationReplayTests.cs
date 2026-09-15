using Causalia.Tracing;

namespace Causalia.Tests.Runtime;

[TestClass]
public sealed class SimulationReplayTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_WithSameSeed_ProducesSameTrace()
    {
        var first = await RunScenarioAsync(729381, TestContext.CancellationToken);
        var second = await RunScenarioAsync(729381, TestContext.CancellationToken);

        Assert.AreEqual(first.Steps, second.Steps);
        Assert.AreEqual(first.VirtualElapsed, second.VirtualElapsed);
        CollectionAssert.AreEqual(GetMessages(first.Trace), GetMessages(second.Trace));
    }

    [TestMethod]
    public async Task RunAsync_WhenScenarioFails_ExposesReplaySeedAndTrace()
    {
        var options = new SimulationOptions { Seed = 9182771 };

        var exception = await Assert.ThrowsExactlyAsync<Exceptions.SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                options,
                async context =>
                {
                    context.TraceEvent("before-failure");
                    await Task.Yield();
                    throw new InvalidOperationException("boom");
                },
                TestContext.CancellationToken));

        Assert.AreEqual(9182771UL, exception.Seed);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.IsTrue(exception.Trace.Any(entry => entry.Message == "before-failure"));
    }

    private static Task<SimulationResult> RunScenarioAsync(ulong seed, CancellationToken cancellationToken)
    {
        var options = new SimulationOptions { Seed = seed };

        return Simulation.RunAsync(
            options,
            context => context.ConcurrentAsync(
                async operationCancellationToken =>
                {
                    for (var index = 0; index < 4; index++)
                    {
                        operationCancellationToken.ThrowIfCancellationRequested();
                        context.TraceEvent($"left:{index}");
                        await Task.Yield();
                    }
                },
                async operationCancellationToken =>
                {
                    for (var index = 0; index < 4; index++)
                    {
                        operationCancellationToken.ThrowIfCancellationRequested();
                        context.TraceEvent($"right:{index}");
                        await Task.Yield();
                    }
                },
                cancellationToken),
            cancellationToken);
    }

    private static List<string> GetMessages(IReadOnlyList<SimulationTraceEntry> trace)
    {
        return trace.Select(entry => entry.Message).ToList();
    }
    [TestMethod]
    public async Task TraceSchedulerEventsFalseShouldKeepScheduleCaptureAndReplay()
    {
        var options = new SimulationOptions
        {
            Seed = 7007,
            TraceSchedulerEvents = false
        };

        [DeterministicSimulation]
        static async Task ScenarioAsync(SimulationContext context)
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
        }

        var result = await Simulation.RunAsync(options, ScenarioAsync, TestContext.CancellationToken);

        Assert.IsTrue(result.Schedule.Decisions.Count > 0);
        Assert.IsFalse(result.Trace.Any(entry => entry.Message.StartsWith("scheduler:", StringComparison.Ordinal)));

        var replay = await Simulation.ReplayAsync(options, result.Schedule, ScenarioAsync, TestContext.CancellationToken);
        Assert.AreEqual(result.Schedule.ReplayToken, replay.Schedule.ReplayToken);
    }

}
