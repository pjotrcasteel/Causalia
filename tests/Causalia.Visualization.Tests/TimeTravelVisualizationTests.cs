using Causalia.TimeTravel;
using Causalia.Visualization;

namespace Causalia.Visualization.Tests;

[TestClass]
public sealed class TimeTravelVisualizationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateDocument_WithTimeTravel_ExportsCheckpointsAndState()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions
            {
                TimeTravel = new TimeTravelOptions { CaptureMode = TimeTravelCaptureMode.SchedulerSteps }
            },
            async context =>
            {
                var value = 0;
                context.TimeTravel.Watch("counter", () => value.ToString());
                await Task.Yield();
                value = 1;
            },
            TestContext.CancellationToken);

        var document = result.ToTraceDocument();
        var html = result.ToTraceHtml();

        Assert.AreEqual(result.TimeTravel.Checkpoints.Count, document.TimeTravel.Count);
        Assert.AreEqual(result.TimeTravel.Checkpoints.Count, document.Summary.TimeTravelCheckpointCount);
        Assert.IsTrue(document.TimeTravel.Any(value => value.State.Any(state => state.Name == "counter")));
        Assert.IsTrue(html.Contains("tt1:", StringComparison.Ordinal));
        Assert.IsTrue(html.Contains("timeTravelCheckpoint", StringComparison.Ordinal));
    }
}
