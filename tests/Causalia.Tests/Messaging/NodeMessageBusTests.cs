using Causalia.Messaging;
using Causalia.Messaging.Faults;

namespace Causalia.Tests.Messaging;

[TestClass]
public sealed class NodeMessageBusTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NodeHandler_WhenNodeIsCrashedBeforeDelivery_WaitsForRestart()
    {
        var handledAt = DateTimeOffset.MinValue;
        var startTime = new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 701, StartTime = startTime },
            async context =>
            {
                var node = context.CreateNode("worker");
                var bus = context.CreateMessageBus();
                bus.RegisterNodeHandler<TestMessage>(
                    node,
                    (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        handledAt = context.TimeProvider.GetUtcNow();
                        return Task.CompletedTask;
                    });

                node.Crash();
                var completion = bus.SendAndWaitAsync("worker", new TestMessage("hello"), context.CancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(10), context.TimeProvider, context.CancellationToken);
                node.Restart();
                await completion;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(startTime.AddSeconds(10), handledAt);
        Assert.IsTrue(result.Trace.Any(entry => entry.Message.StartsWith("node:restarted:worker:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task NodeHandler_WhenNodeCrashesDuringDelivery_RedeliversAfterRestart()
    {
        var attempts = new List<int>();

        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 702 },
            async context =>
            {
                var node = context.CreateNode("worker");
                var bus = context.CreateMessageBus();
                bus.RegisterNodeHandler<TestMessage>(
                    node,
                    async (_, delivery, cancellationToken) =>
                    {
                        attempts.Add(delivery.Attempt);
                        await Task.Delay(TimeSpan.FromSeconds(10), context.TimeProvider, cancellationToken);
                    });

                var completion = bus.SendAndWaitAsync("worker", new TestMessage("hello"), context.CancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), context.TimeProvider, context.CancellationToken);
                node.Crash();
                await Task.Delay(TimeSpan.FromSeconds(5), context.TimeProvider, context.CancellationToken);
                node.Restart();
                await completion;
            },
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(new List<int> { 1, 2 }, attempts);
        Assert.IsTrue(result.Trace.Any(entry => entry.Message.StartsWith("messaging:interrupted:1:1:", StringComparison.Ordinal)));
        Assert.IsTrue(result.Trace.Any(entry => entry.Message.StartsWith("messaging:completed:1:2:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task NodeHandler_WithDuplicateAndCrash_UsesUniqueDeliveryAttempts()
    {
        var attempts = new List<int>();
        var firstDelivery = true;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 703 },
            async context =>
            {
                var node = context.CreateNode("worker");
                var faults = new MessageFaultPlan().Duplicate(1.0);
                var bus = context.CreateMessageBus(new MessageBusOptions { Faults = faults });
                bus.RegisterNodeHandler<TestMessage>(
                    node,
                    async (_, delivery, cancellationToken) =>
                    {
                        attempts.Add(delivery.Attempt);

                        if (firstDelivery)
                        {
                            firstDelivery = false;
                            await Task.Delay(TimeSpan.FromSeconds(10), context.TimeProvider, cancellationToken);
                        }
                    });

                var completion = bus.SendAndWaitAsync("worker", new TestMessage("hello"), context.CancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), context.TimeProvider, context.CancellationToken);
                node.Crash();
                node.Restart();
                await completion;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(attempts.Count, attempts.Distinct().Count());
        CollectionAssert.AreEquivalent(new List<int> { 1, 2, 3 }, attempts);
    }

    private sealed record TestMessage(string Value);
}
