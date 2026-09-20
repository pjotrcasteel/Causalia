namespace Causalia.Tests.Time;

[TestClass]
public sealed class SimulationTimeProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(-1L)]
    [DataRow(-5_000L)]
    [DataRow(-10_001L)]
    public async Task CreateTimer_WithNegativeNonInfiniteInterval_RejectsWithoutMovingTime(long ticks)
    {
        var result = await Simulation.RunAsync(context =>
        {
            var negative = TimeSpan.FromTicks(ticks);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                context.TimeProvider.CreateTimer(_ => { }, null, negative, Timeout.InfiniteTimeSpan));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                context.TimeProvider.CreateTimer(_ => { }, null, TimeSpan.Zero, negative));
            return Task.CompletedTask;
        }, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.Zero, result.VirtualElapsed);
    }

    [TestMethod]
    public async Task CreateTimer_WithInfiniteDueTime_RemainsDisabled()
    {
        var callbacks = 0;
        await Simulation.RunAsync(async context =>
        {
            using var timer = context.TimeProvider.CreateTimer(_ => callbacks++, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
        }, TestContext.CancellationToken);

        Assert.AreEqual(0, callbacks);
    }

    [TestMethod]
    public async Task RunAsync_WithVirtualDelays_AdvancesToLatestDueTime()
    {
        var options = new SimulationOptions
        {
            Seed = 42,
            StartTime = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var result = await Simulation.RunAsync(
            options,
            context => context.ConcurrentAsync(
                async cancellationToken =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), context.TimeProvider, cancellationToken);
                    context.TraceEvent("ten-seconds");
                },
                async cancellationToken =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), context.TimeProvider, cancellationToken);
                    context.TraceEvent("five-seconds");
                },
                context.CancellationToken),
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromSeconds(10), result.VirtualElapsed);
        Assert.AreEqual(
            options.StartTime.AddSeconds(5),
            result.Trace.Single(entry => entry.Message == "five-seconds").Timestamp);
        Assert.AreEqual(
            options.StartTime.AddSeconds(10),
            result.Trace.Single(entry => entry.Message == "ten-seconds").Timestamp);
    }
}
