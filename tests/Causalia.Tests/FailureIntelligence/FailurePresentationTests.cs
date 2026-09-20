using Causalia.Exceptions;

namespace Causalia.Tests.FailureIntelligence;

[TestClass]
public sealed class FailurePresentationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SimulationFailure_WhenInvariantFails_ExplainsFailureAndReplay()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                async context =>
                {
                    var processed = 0;
                    context.Invariant("order happens once", () => processed <= 1);
                    context.TraceEvent("order received");
                    processed++;
                    context.TraceEvent("retry started");
                    processed++;
                    await Task.Yield();
                },
                TestContext.CancellationToken));

        StringAssert.StartsWith(exception.Message, "CAUSALIA FAILURE");
        StringAssert.Contains(exception.Message, "Invariant: order happens once");
        StringAssert.Contains(exception.Message, "Observed:");
        StringAssert.Contains(exception.Message, "Failure sequence:");
        StringAssert.Contains(exception.Message, "order received");
        StringAssert.Contains(exception.Message, "retry started");
        StringAssert.Contains(exception.Message, "Replay:");
        StringAssert.Contains(exception.Message, "Simulation.ReplayAsync");
    }

    [TestMethod]
    public async Task SimulationFailure_WhenUnhandledExceptionOccurs_ShowsExceptionWithoutClaimingFaultCause()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                context =>
                {
                    context.TraceEvent("before external call");
                    throw new InvalidOperationException("remote side effect uncertain");
                },
                TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "Failure: InvalidOperationException");
        StringAssert.Contains(exception.Message, "Observed: remote side effect uncertain");
        StringAssert.Contains(exception.Message, "before external call");
    }
}
