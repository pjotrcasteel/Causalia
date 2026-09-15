using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.Visualization;

namespace Causalia.Visualization.Tests;

[TestClass]
public sealed class FailureIntelligenceVisualizationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ToTraceDocument_WithFailureAnalysis_ExposesSignatureTriggerAndReproduction()
    {
        var options = new SimulationOptions { Seed = 9901 };
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(options, FailureScenarioAsync, TestContext.CancellationToken));
        var analysis = await Simulation.AnalyzeFailureAsync(
            options,
            new FailureAnalysisOptions(),
            failure,
            FailureScenarioAsync,
            TestContext.CancellationToken);

        var document = analysis.ToTraceDocument();

        Assert.AreEqual(analysis.Signature.Token, document.Summary.FailureSignature);
        Assert.AreEqual(analysis.Kind, document.Summary.FailureKind);
        Assert.AreEqual(analysis.Trigger, document.Summary.FailureTrigger);
        Assert.AreEqual(analysis.Confidence, document.Summary.FailureConfidence);
        Assert.AreEqual(analysis.ReproductionToken, document.Summary.ReproductionToken);
    }

    [TestMethod]
    public async Task ToTraceHtml_WithFailureAnalysis_RendersFailureIntelligenceCards()
    {
        var options = new SimulationOptions { Seed = 9902 };
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(options, FailureScenarioAsync, TestContext.CancellationToken));
        var analysis = await Simulation.AnalyzeFailureAsync(
            options,
            new FailureAnalysisOptions(),
            failure,
            FailureScenarioAsync,
            TestContext.CancellationToken);

        var html = analysis.ToTraceHtml();

        StringAssert.Contains(html, analysis.Signature.Token);
        StringAssert.Contains(html, nameof(FailureTrigger.Deterministic));
        StringAssert.Contains(html, analysis.ReproductionToken!);
    }

    private static Task FailureScenarioAsync(SimulationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("visualization-failure");
    }
}
