using Causalia.Messaging;
using Causalia.Messaging.Faults;

namespace Causalia.Tests.Messaging;

[TestClass]
public sealed class MessageFaultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendAsync_WithDropFault_DoesNotInvokeHandler()
    {
        var invocationCount = 0;
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 101 },
            async context =>
            {
                var bus = context.CreateMessageBus(new MessageBusOptions { Faults = new MessageFaultPlan().Drop(1) });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        invocationCount++;
                        return Task.CompletedTask;
                    });

                await bus.SendAsync("worker", new TestMessage(1), context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(0, invocationCount);
        Assert.IsTrue(result.Trace.Any(entry => entry.Message.StartsWith("fault:messaging:drop:1:worker", StringComparison.Ordinal)));
        Assert.IsTrue(result.Trace.Any(entry => entry.Message.StartsWith("messaging:dropped:1:worker", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SendAndWaitAsync_WithDuplicateFault_DeliversSameMessageIdWithIncrementingAttempts()
    {
        var deliveries = new List<MessageDeliveryContext>();
        var retainedAttemptStates = -1;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 102 },
            async context =>
            {
                var bus = context.CreateMessageBus(new MessageBusOptions { Faults = new MessageFaultPlan().Duplicate(1) });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    (_, delivery, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        deliveries.Add(delivery);
                        return Task.CompletedTask;
                    });

                await bus.SendAndWaitAsync("worker", new TestMessage(1), context.CancellationToken);
                retainedAttemptStates = bus.DeliveryAttemptStateCount;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(2, deliveries.Count);
        Assert.AreEqual(deliveries[0].MessageId, deliveries[1].MessageId);
        CollectionAssert.AreEqual(new List<int> { 1, 2 }, deliveries.Select(delivery => delivery.Attempt).ToList());
        Assert.AreEqual(0, retainedAttemptStates);
    }

    [TestMethod]
    public async Task SendAsync_WithDelayFault_AddsVirtualLatency()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 103 },
            async context =>
            {
                var bus = context.CreateMessageBus(
                    new MessageBusOptions
                    {
                        DeliveryLatency = TimeSpan.FromSeconds(10),
                        Faults = new MessageFaultPlan().Delay(1, TimeSpan.FromSeconds(30))
                    });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    });

                await bus.SendAsync("worker", new TestMessage(1), context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromSeconds(40), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task SendAsync_WithReorderFault_AllowsLaterMessageToOvertakePendingMessage()
    {
        var received = new List<int>();

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 104 },
            async context =>
            {
                var bus = context.CreateMessageBus(new MessageBusOptions { Faults = new MessageFaultPlan().Reorder(1) });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    (message, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        received.Add(message.Value);
                        return Task.CompletedTask;
                    });

                await bus.SendAsync("worker", new TestMessage(1), context.CancellationToken);
                await bus.SendAsync("worker", new TestMessage(2), context.CancellationToken);
            },
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(new List<int> { 2, 1 }, received);
    }

    [TestMethod]
    public async Task Faults_WithSameSeed_ProduceSameTrace()
    {
        var first = await RunFaultScenarioAsync(105, TestContext.CancellationToken);
        var second = await RunFaultScenarioAsync(105, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            first.Trace.Select(entry => entry.Message).ToList(),
            second.Trace.Select(entry => entry.Message).ToList());
    }

    private static Task<SimulationResult> RunFaultScenarioAsync(ulong seed, CancellationToken cancellationToken)
    {
        return Simulation.RunAsync(
            new SimulationOptions { Seed = seed },
            async context =>
            {
                var faults = new MessageFaultPlan()
                    .Drop(0.2)
                    .Duplicate(0.3)
                    .Delay(0.4, TimeSpan.FromMilliseconds(25))
                    .Reorder(0.5);
                var bus = context.CreateMessageBus(new MessageBusOptions { Faults = faults });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    (_, _, handlerCancellationToken) =>
                    {
                        handlerCancellationToken.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    });

                for (var index = 0; index < 12; index++)
                {
                    await bus.SendAsync("worker", new TestMessage(index), context.CancellationToken);
                }
            },
            cancellationToken);
    }

    private sealed record TestMessage(int Value);
}
