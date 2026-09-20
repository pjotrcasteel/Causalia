using Causalia.Exceptions;
using Causalia.Invariants;

namespace Causalia.Tests.Runtime;

[TestClass]
public sealed class SimulationAdoptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_WithoutOptions_UsesDefaultDeterministicOptions()
    {
        var result = await Simulation.RunAsync(
            context =>
            {
                Assert.AreEqual(1UL, context.Seed);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1UL, result.Seed);
    }

    [TestMethod]
    public async Task Invariant_WhenConditionRemainsTrue_CompletesSuccessfully()
    {
        var value = 0;

        var result = await Simulation.RunAsync(
            async context =>
            {
                context.Invariant("value never becomes negative", () => value >= 0);
                value++;
                await Task.Yield();
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.Invariants.Count);
        Assert.AreEqual("value never becomes negative", result.Invariants[0].Name);
    }

    [TestMethod]
    public async Task Invariant_WhenConditionBecomesFalse_FailsSimulation()
    {
        var value = 0;

        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                async context =>
                {
                    context.Invariant("value never becomes negative", () => value >= 0);
                    value = -1;
                    await Task.Yield();
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantViolationException>(exception.InnerException);
        var invariantException = (SimulationInvariantViolationException)exception.InnerException!;
        Assert.AreEqual("value never becomes negative", invariantException.InvariantName);
        Assert.AreEqual(InvariantKind.Always, invariantException.Kind);
    }
}
