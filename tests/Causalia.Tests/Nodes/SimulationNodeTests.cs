using Causalia.Nodes;

namespace Causalia.Tests.Nodes;

[TestClass]
public sealed class SimulationNodeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CrashAndRestart_ChangesGenerationAndProducesLifecycleTrace()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 611 },
            context =>
            {
                var node = context.CreateNode("worker");

                Assert.AreEqual(SimulationNodeState.Running, node.State);
                Assert.AreEqual(1, node.Generation);
                Assert.IsTrue(node.Crash());
                Assert.AreEqual(SimulationNodeState.Crashed, node.State);
                Assert.IsFalse(node.Crash());
                Assert.IsTrue(node.Restart());
                Assert.AreEqual(SimulationNodeState.Running, node.State);
                Assert.AreEqual(2, node.Generation);
                Assert.IsFalse(node.Restart());
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.Trace.Any(entry => entry.Message == "node:created:worker:generation:1"));
        Assert.IsTrue(result.Trace.Any(entry => entry.Message == "node:crashed:worker:generation:1"));
        Assert.IsTrue(result.Trace.Any(entry => entry.Message == "node:restarted:worker:generation:2"));
    }

    [TestMethod]
    public async Task RunAsync_WhenNodeCrashes_CancelsGenerationWork()
    {
        var wasCancelled = false;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 612 },
            async context =>
            {
                var node = context.CreateNode("worker");
                var work = node.RunAsync(
                    async operationCancellationToken =>
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1), context.TimeProvider, operationCancellationToken);
                        }
                        catch (OperationCanceledException) when (operationCancellationToken.IsCancellationRequested)
                        {
                            wasCancelled = true;
                        }
                    },
                    context.CancellationToken);

                await Task.Yield();
                node.Crash();
                await work;
            },
            TestContext.CancellationToken);

        Assert.IsTrue(wasCancelled);
    }

    [TestMethod]
    public async Task WaitUntilRunningAsync_WhenNodeRestarts_ContinuesAfterRestart()
    {
        var continued = false;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 613 },
            async context =>
            {
                var node = context.CreateNode("worker");
                node.Crash();
                var waiting = WaitForNodeAsync(node, () => continued = true, context.CancellationToken);

                await Task.Delay(TimeSpan.FromSeconds(5), context.TimeProvider, context.CancellationToken);
                Assert.IsFalse(continued);
                node.Restart();
                await waiting;
            },
            TestContext.CancellationToken);

        Assert.IsTrue(continued);
    }

    [TestMethod]
    public async Task WaitUntilRunningAsync_WhenStoppedNodeCrashes_ContinuesAfterRestart()
    {
        var continued = false;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 614 },
            async context =>
            {
                var node = context.CreateNode("worker");
                Assert.IsTrue(node.Stop());
                var waiting = WaitForNodeAsync(node, () => continued = true, context.CancellationToken);
                Assert.IsTrue(node.Crash());
                Assert.IsTrue(node.Restart());
                await waiting;
            },
            TestContext.CancellationToken);

        Assert.IsTrue(continued);
    }

    private static async Task WaitForNodeAsync(
        SimulationNode node,
        Action onRunning,
        CancellationToken cancellationToken)
    {
        await node.WaitUntilRunningAsync(cancellationToken);
        onRunning();
    }
}
