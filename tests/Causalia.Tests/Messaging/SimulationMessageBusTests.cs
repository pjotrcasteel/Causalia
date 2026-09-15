using Causalia.Exceptions;
using Causalia.Messaging;
using Causalia.Messaging.Exceptions;

namespace Causalia.Tests.Messaging;

[TestClass]
public sealed class SimulationMessageBusTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendAsync_WithDeliveryLatency_DeliversAtExpectedVirtualTime()
    {
        DateTimeOffset? deliveredAt = null;
        MessageDeliveryContext? observedContext = null;
        var startTime = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var options = new SimulationOptions { Seed = 17, StartTime = startTime };

        var result = await Simulation.RunAsync(
            options,
            async context =>
            {
                var bus = context.CreateMessageBus(new MessageBusOptions { DeliveryLatency = TimeSpan.FromSeconds(30) });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    (message, deliveryContext, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Assert.AreEqual("hello", message.Value);
                        observedContext = deliveryContext;
                        deliveredAt = context.TimeProvider.GetUtcNow();
                        return Task.CompletedTask;
                    });

                await bus.SendAsync("worker", new TestMessage("hello"), context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromSeconds(30), result.VirtualElapsed);
        Assert.AreEqual(startTime.AddSeconds(30), deliveredAt);
        Assert.IsNotNull(observedContext);
        Assert.AreEqual(1L, observedContext!.MessageId);
        Assert.AreEqual("worker", observedContext.Endpoint);
        Assert.AreEqual(1, observedContext.Attempt);
    }

    [TestMethod]
    public async Task SendAsync_WithMultipleMessages_PreservesEndpointFifoWithoutMultiplyingLatency()
    {
        var received = new List<int>();
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 23 },
            async context =>
            {
                var bus = context.CreateMessageBus(new MessageBusOptions { DeliveryLatency = TimeSpan.FromSeconds(10) });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    async (message, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        received.Add(int.Parse(message.Value));
                        await Task.Yield();
                    });

                var first = bus.SendAsync("worker", new TestMessage("1"), context.CancellationToken);
                var second = bus.SendAsync("worker", new TestMessage("2"), context.CancellationToken);
                var third = bus.SendAsync("worker", new TestMessage("3"), context.CancellationToken);
                await Task.WhenAll(first, second, third);
            },
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, received);
        Assert.AreEqual(TimeSpan.FromSeconds(10), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task SendAndWaitAsync_CompletesAfterHandlerFinishes()
    {
        var handlerCompleted = false;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 29 },
            async context =>
            {
                var bus = context.CreateMessageBus();
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    async (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                        handlerCompleted = true;
                    });

                await bus.SendAndWaitAsync("worker", new TestMessage("hello"), context.CancellationToken);
                Assert.IsTrue(handlerCompleted);
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SendAsync_WithSameSeed_ProducesSameMessagingTrace()
    {
        var first = await RunMessagingScenarioAsync(91, TestContext.CancellationToken);
        var second = await RunMessagingScenarioAsync(91, TestContext.CancellationToken);
        var firstTrace = first.Trace.Select(entry => entry.Message).ToList();
        var secondTrace = second.Trace.Select(entry => entry.Message).ToList();

        CollectionAssert.AreEqual(firstTrace, secondTrace);
    }

    [TestMethod]
    public async Task RegisterHandler_WithDuplicateRegistration_ExposesConfigurationFailure()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 41 },
                context =>
                {
                    var bus = context.CreateMessageBus();
                    bus.RegisterHandler<TestMessage>(
                        "worker",
                        (_, _, cancellationToken) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            return Task.CompletedTask;
                        });
                    bus.RegisterHandler<TestMessage>(
                        "worker",
                        (_, _, cancellationToken) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            return Task.CompletedTask;
                        });
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<DuplicateMessageHandlerException>(exception.InnerException);
    }

    [TestMethod]
    public async Task SendAsync_WhenHandlerFails_PropagatesHandlerFailure()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 43 },
                async context =>
                {
                    var bus = context.CreateMessageBus();
                    bus.RegisterHandler<TestMessage>(
                        "worker",
                        (_, _, cancellationToken) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new InvalidOperationException("handler failed");
                        });
                    await bus.SendAsync("worker", new TestMessage("hello"), context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual("handler failed", exception.InnerException!.Message);
    }

    [TestMethod]
    public async Task SendAsync_WithoutHandler_ExposesFailureThroughSimulation()
    {
        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 42 },
                async context =>
                {
                    var bus = context.CreateMessageBus();
                    await bus.SendAsync("missing", new TestMessage("hello"), context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<MessageHandlerNotFoundException>(exception.InnerException);
    }

    private static Task<SimulationResult> RunMessagingScenarioAsync(ulong seed, CancellationToken cancellationToken)
    {
        return Simulation.RunAsync(
            new SimulationOptions { Seed = seed },
            async context =>
            {
                var bus = context.CreateMessageBus(new MessageBusOptions { DeliveryLatency = TimeSpan.FromMilliseconds(5) });
                bus.RegisterHandler<TestMessage>(
                    "worker",
                    async (_, _, handlerCancellationToken) =>
                    {
                        handlerCancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                    });

                var first = bus.SendAsync("worker", new TestMessage("a"), context.CancellationToken);
                var second = bus.SendAsync("worker", new TestMessage("b"), context.CancellationToken);
                await Task.WhenAll(first, second);
            },
            cancellationToken);
    }

    private sealed record TestMessage(string Value);
}
