namespace Causalia.Tests.Time;

[TestClass]
public sealed class SimulationTimeProviderTests
{
    public TestContext TestContext { get; set; } = null!;

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
