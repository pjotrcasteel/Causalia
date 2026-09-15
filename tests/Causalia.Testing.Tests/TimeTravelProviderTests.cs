using Causalia.Exceptions;
using Causalia.TimeTravel;

namespace Causalia.Testing.Tests;

[TestClass]
public sealed class TimeTravelProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_RetainsSuccessfulTimeTravelTimeline()
    {
        var provider = new SimulationProvider();
        provider.Configure(
            new SimulationOptions
            {
                TimeTravel = new TimeTravelOptions { CaptureMode = TimeTravelCaptureMode.SchedulerSteps }
            });

        var result = await provider.RunAsync(
            async context =>
            {
                context.TimeTravel.Watch("state", static () => "running");
                await Task.Yield();
            },
            TestContext.CancellationToken);

        Assert.AreSame(result.TimeTravel, provider.LastTimeTravel);
    }

    [TestMethod]
    public async Task RunAsync_WhenFailureOccurs_RetainsFailureTimeTravelTimeline()
    {
        var provider = new SimulationProvider();
        provider.Configure(
            new SimulationOptions
            {
                TimeTravel = new TimeTravelOptions { CaptureMode = TimeTravelCaptureMode.SchedulerSteps }
            });
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                async context =>
                {
                    context.TimeTravel.Watch("state", static () => "broken");
                    await Task.Yield();
                    throw new InvalidOperationException("boom");
                },
                TestContext.CancellationToken));

        Assert.AreSame(failure.TimeTravel, provider.LastTimeTravel);
        Assert.AreEqual(TimeTravelCheckpointKind.Failure, provider.LastTimeTravel?.Last?.Kind);
    }
}
