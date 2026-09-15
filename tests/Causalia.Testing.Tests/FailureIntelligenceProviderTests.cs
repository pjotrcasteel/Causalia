using Causalia;
using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.Testing;

namespace Causalia.Testing.Tests;

[TestClass]
public sealed class FailureIntelligenceProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_WhenSimulationFails_RetainsDescriptiveFailureAnalysis()
    {
        var provider = new SimulationProvider();

        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                context => ThrowAsync(context, "provider-failure"),
                TestContext.CancellationToken));

        Assert.IsNotNull(provider.LastFailureAnalysis);
        Assert.AreEqual(failure.Signature.Token, provider.LastFailureAnalysis.Signature.Token);
        Assert.AreEqual(FailureTrigger.Unknown, provider.LastFailureAnalysis.Trigger);
    }

    [TestMethod]
    public async Task AnalyzeFailureAsync_RetainsMinimizedAnalysisAndRepresentativeFailure()
    {
        var provider = new SimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 8801 });
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                context => ThrowAsync(context, "provider-failure"),
                TestContext.CancellationToken));

        var analysis = await provider.AnalyzeFailureAsync(
            new FailureAnalysisOptions(),
            failure,
            context => ThrowAsync(context, "provider-failure"),
            TestContext.CancellationToken);

        Assert.AreSame(analysis, provider.LastFailureAnalysis);
        Assert.AreSame(analysis.Minimization, provider.LastMinimizationResult);
        Assert.AreSame(analysis.RepresentativeFailure, provider.LastFailure);
        Assert.AreEqual(FailureTrigger.Deterministic, analysis.Trigger);
    }

    private static Task ThrowAsync(SimulationContext context, string message)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(message);
    }
}
