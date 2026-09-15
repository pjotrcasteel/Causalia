using Causalia.Exceptions;
using Causalia.Visualization;

namespace Causalia.Visualization.Tests;

[TestClass]
public sealed class SimulationTraceVisualizationTests
{
    [TestMethod]
    public async Task CreateDocumentClassifiesSchedulerAndUserEvents()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 42 },
            async context =>
            {
                context.TraceEvent("checkout:start");
                context.TraceEvent("checkout:not-failed");
                await Task.Yield();
                context.TraceEvent("checkout:complete");
            },
            TestContext.CancellationToken);

        var document = result.ToTraceDocument();

        Assert.IsTrue(document.Events.Any(traceEvent => traceEvent.Category == TraceEventCategory.User));
        Assert.IsTrue(document.Events.Any(traceEvent => traceEvent.Category == TraceEventCategory.Scheduler));
        Assert.AreEqual(
            TraceEventSeverity.Information,
            document.Events.Single(traceEvent => traceEvent.Message == "checkout:not-failed").Severity);
        Assert.AreEqual(result.Schedule.ReplayToken, document.Summary.ReplayToken);
    }

    [TestMethod]
    public async Task CreateDocumentIncludesFailureMetadata()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(
            () => Simulation.RunAsync(
                new SimulationOptions { Seed = 7 },
                context =>
                {
                    context.TraceEvent("scenario:before-failure");
                    throw new InvalidOperationException("boom");
                },
                TestContext.CancellationToken));

        var document = failure.ToTraceDocument();

        Assert.AreEqual(nameof(InvalidOperationException), document.Summary.FailureType);
        Assert.AreEqual("boom", document.Summary.FailureMessage);
    }

    [TestMethod]
    public async Task ExportCreatesSelfContainedInteractiveHtml()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 91 },
            context =>
            {
                context.TraceEvent("node:created:orders:generation:1");
                context.TraceEvent("messaging:enqueued:42:1:worker:Example.Message");
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        var html = result.ToTraceHtml(new TraceHtmlOptions { Title = "My Trace" });

        StringAssert.Contains(html, "My Trace");
        StringAssert.Contains(html, "message:42");
        StringAssert.Contains(html, result.Schedule.ReplayToken);
        Assert.IsFalse(html.Contains("https://", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("http://", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task CreateDocumentClassifiesConsistencyEventsAndHistoryLane()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 93 },
            context =>
            {
                context.TraceEvent("consistency:orders:write:7:session:checkout:replica:primary:key:order-42");
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        var document = result.ToTraceDocument();
        var consistency = document.Events.Single(traceEvent => traceEvent.Category == TraceEventCategory.Consistency);

        Assert.AreEqual("consistency/orders", consistency.Lane);
        Assert.AreEqual("consistency:orders:version:7", consistency.CorrelationId);
    }

    [TestMethod]
    public async Task CreateDocumentClassifiesModelEventsAndCommandCorrelation()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 94 },
            context =>
            {
                context.TraceEvent("model:checkout:command:2:verified:confirm");
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        var document = result.ToTraceDocument();
        var modelEvent = document.Events.Single(traceEvent => traceEvent.Category == TraceEventCategory.ModelBased);

        Assert.AreEqual("model/checkout", modelEvent.Lane);
        Assert.AreEqual("model:checkout:command:2", modelEvent.CorrelationId);
    }

    [TestMethod]
    public async Task CreateDocumentClassifiesLoadEventsAndIterationCorrelation()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 92 },
            context =>
            {
                context.TraceEvent("load:run:started:constant-arrival-rate");
                context.TraceEvent("load:iteration:started:42:actor:7");
                context.TraceEvent("load:iteration:completed:42:actor:7");
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        var document = result.ToTraceDocument();
        var iterations = document.Events.Where(traceEvent => traceEvent.CorrelationId == "load:42").ToArray();

        Assert.AreEqual(2, iterations.Length);
        Assert.IsTrue(iterations.All(traceEvent => traceEvent.Category == TraceEventCategory.Load));
        Assert.IsTrue(iterations.All(traceEvent => traceEvent.Lane == "load/actor/7"));
    }

    public TestContext TestContext { get; set; } = null!;
}
