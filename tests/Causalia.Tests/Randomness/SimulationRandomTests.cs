namespace Causalia.Tests.Randomness;

[TestClass]
public sealed class SimulationRandomTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_WithSameSeed_ProducesSameUserRandomSequence()
    {
        var first = await CaptureAsync(729381, TestContext.CancellationToken);
        var second = await CaptureAsync(729381, TestContext.CancellationToken);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task RunAsync_UserRandomness_DoesNotChangeSchedulerSchedule()
    {
        var withoutRandom = await CaptureScheduleAsync(useRandom: false, TestContext.CancellationToken);
        var withRandom = await CaptureScheduleAsync(useRandom: true, TestContext.CancellationToken);

        Assert.AreEqual(withoutRandom.ReplayToken, withRandom.ReplayToken);
    }

    private static async Task<string[]> CaptureAsync(ulong seed, CancellationToken cancellationToken)
    {
        string[] values = [];

        await Simulation.RunAsync(
            new SimulationOptions { Seed = seed },
            context =>
            {
                values =
                [
                    context.Random.NextInt32(100).ToString(),
                    context.Random.NextUInt64().ToString(),
                    context.Random.NextBoolean().ToString(),
                    context.Random.NextDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    context.Random.NextGuid().ToString("D")
                ];
                return Task.CompletedTask;
            },
            cancellationToken);

        return values;
    }

    private static async Task<Causalia.Scheduling.SimulationSchedule> CaptureScheduleAsync(
        bool useRandom,
        CancellationToken cancellationToken)
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 42 },
            async context =>
            {
                if (useRandom)
                {
                    _ = context.Random.NextUInt64();
                    _ = context.Random.NextUInt64();
                }

                await context.ConcurrentAsync(
                    async operationCancellationToken =>
                    {
                        await Task.Yield();
                        operationCancellationToken.ThrowIfCancellationRequested();
                    },
                    async operationCancellationToken =>
                    {
                        await Task.Yield();
                        operationCancellationToken.ThrowIfCancellationRequested();
                    },
                    context.CancellationToken);
            },
            cancellationToken);

        return result.Schedule;
    }
}
