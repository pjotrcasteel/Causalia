using Causalia.AzureServiceBus;
using Causalia.Faults;

namespace Causalia.Ecosystem.Tests;

[TestClass]
public sealed class AzureServiceBusSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LockExpiry_RedeliversWithIncrementedCount_AndInvalidatesOldToken()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateQueue("orders", TimeSpan.FromSeconds(2));
            await broker.SendAsync("orders", new byte[] { 1 }, null, context.CancellationToken);
            var first = await broker.ReceiveAsync("orders", context.CancellationToken);
            Assert.IsNotNull(first);
            await Task.Delay(TimeSpan.FromSeconds(3), context.TimeProvider, context.CancellationToken);
            var second = await broker.ReceiveAsync("orders", context.CancellationToken);
            Assert.IsNotNull(second);
            Assert.AreEqual(2, second.DeliveryCount);
            Assert.AreNotEqual(first.LockToken, second.LockToken);
            try
            {
                await broker.CompleteAsync("orders", first, context.CancellationToken);
                Assert.Fail("Expired lock was accepted.");
            }
            catch (SimulationServiceBusException exception)
            {
                Assert.IsFalse(exception.SettlementCommitted);
            }
            await broker.CompleteAsync("orders", second, context.CancellationToken);
            Assert.IsNull(await broker.ReceiveAsync("orders", context.CancellationToken));
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task LostCompleteAcknowledgement_DoesNotRedeliverCommittedMessage()
    {
        await Simulation.RunAsync(async context =>
        {
            var faults = new FaultPlan<ServiceBusSettlementEvent, ServiceBusSettlementFault>();
            faults.On<ServiceBusSettlementEvent>("lost-complete")
                .Where(value => value.Action == ServiceBusSettlementAction.Complete).Once()
                .Apply(_ => ServiceBusSettlementFault.LoseAcknowledgement);
            var broker = context.CreateAzureServiceBus(faults);
            broker.CreateQueue("orders");
            await broker.SendAsync("orders", new byte[] { 1 }, null, context.CancellationToken);
            var delivery = await broker.ReceiveAsync("orders", context.CancellationToken);
            Assert.IsNotNull(delivery);
            try
            {
                await broker.CompleteAsync("orders", delivery, context.CancellationToken);
                Assert.Fail("Lost acknowledgement was observable as success.");
            }
            catch (SimulationServiceBusException exception)
            {
                Assert.IsTrue(exception.SettlementCommitted);
            }
            Assert.IsNull(await broker.ReceiveAsync("orders", context.CancellationToken));
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task TopicSubscriptionsAndScheduledDelivery_AreIndependent()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateTopic("events");
            broker.CreateSubscription("events", "first");
            broker.CreateSubscription("events", "second");
            await broker.SendAsync("events", new byte[] { 5 },
                context.TimeProvider.GetUtcNow().AddSeconds(1), context.CancellationToken);
            Assert.IsNull(await broker.ReceiveAsync("events/subscriptions/first", context.CancellationToken));
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
            var first = await broker.ReceiveAsync("events/subscriptions/first", context.CancellationToken);
            var second = await broker.ReceiveAsync("events/subscriptions/second", context.CancellationToken);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            await broker.DeadLetterAsync("events/subscriptions/first", first, context.CancellationToken);
            await broker.AbandonAsync("events/subscriptions/second", second, context.CancellationToken);
            Assert.AreEqual(1, broker.DeadLetters("events/subscriptions/first").Count);
            Assert.AreEqual(2, (await broker.ReceiveAsync("events/subscriptions/second", context.CancellationToken))!.DeliveryCount);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task RenewedLock_SurvivesOriginalExpiry_AndAbandonRedelivers()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateQueue("orders", TimeSpan.FromSeconds(2));
            await broker.SendAsync("orders", new byte[] { 7 }, null, context.CancellationToken);
            var delivery = (await broker.ReceiveAsync("orders", context.CancellationToken))!;
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
            var renewed = await broker.RenewLockAsync("orders", delivery, context.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
            Assert.IsNull(await broker.ReceiveAsync("orders", context.CancellationToken));
            await broker.AbandonAsync("orders", renewed, context.CancellationToken);
            Assert.AreEqual(2, (await broker.ReceiveAsync("orders", context.CancellationToken))!.DeliveryCount);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task MaxDeliveryCount_MovesMessageToDeadLetters()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateQueue("orders", maxDeliveryCount: 2);
            await broker.SendAsync("orders", new byte[] { 1 }, null, context.CancellationToken);
            for (var index = 0; index < 2; index++)
            {
                var delivery = (await broker.ReceiveAsync("orders", context.CancellationToken))!;
                await broker.AbandonAsync("orders", delivery, context.CancellationToken);
            }
            Assert.IsNull(await broker.ReceiveAsync("orders", context.CancellationToken));
            Assert.AreEqual(2, broker.DeadLetters("orders")[0].DeliveryCount);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task DeliveryTrace_IsStableAcrossIdenticalRuns()
    {
        var first = await RunTraceAsync(TestContext.CancellationToken);
        var second = await RunTraceAsync(TestContext.CancellationToken);
        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task CancelledSend_DoesNotEnqueue()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateQueue("orders");
            using var source = new CancellationTokenSource();
            source.Cancel();
            try
            {
                await broker.SendAsync("orders", new byte[] { 1 }, null, source.Token);
                Assert.Fail("Cancelled send succeeded.");
            }
            catch (OperationCanceledException)
            {
                Assert.IsNull(await broker.ReceiveAsync("orders", context.CancellationToken));
            }
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SessionCloseAfterProcessRestart_ReleasesUnsettledMessage()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateQueue("orders");
            await broker.SendAsync("orders", new byte[] { 1 }, null, "customer-42", context.CancellationToken);
            await broker.SendAsync("orders", new byte[] { 2 }, null, "customer-42", context.CancellationToken);
            await using (var first = await broker.AcceptSessionAsync("orders", "customer-42", context.CancellationToken))
            {
                var delivery = await first.ReceiveAsync(context.CancellationToken);
                Assert.IsNotNull(delivery);
                Assert.IsNull(await first.ReceiveAsync(context.CancellationToken));
                // Volatile receiver is disposed during restart; broker state survives.
            }

            await using var restarted = await broker.AcceptSessionAsync("orders", "customer-42", context.CancellationToken);
            var redelivery = (await restarted.ReceiveAsync(context.CancellationToken))!;
            Assert.AreEqual(2, redelivery.DeliveryCount);
            await restarted.CompleteAsync(redelivery, context.CancellationToken);
            var next = (await restarted.ReceiveAsync(context.CancellationToken))!;
            Assert.AreEqual(1, next.DeliveryCount);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SessionOwnershipExpiresAndCanBeRenewed()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateQueue("orders", TimeSpan.FromSeconds(2));
            await broker.SendAsync("orders", new byte[] { 1 }, null, "customer-42", context.CancellationToken);
            await using var first = await broker.AcceptSessionAsync("orders", "customer-42", context.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
            first.RenewLock();
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
            try
            {
                await broker.AcceptSessionAsync("orders", "customer-42", context.CancellationToken);
                Assert.Fail("Session was accepted while owned.");
            }
            catch (SimulationServiceBusException)
            {
                // Expected exclusive ownership.
            }
            await Task.Delay(TimeSpan.FromSeconds(2), context.TimeProvider, context.CancellationToken);
            await using var second = await broker.AcceptSessionAsync("orders", "customer-42", context.CancellationToken);
            Assert.IsNotNull(await second.ReceiveAsync(context.CancellationToken));
        }, TestContext.CancellationToken);
    }

    private static async Task<string[]> RunTraceAsync(CancellationToken cancellationToken)
    {
        var result = await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateAzureServiceBus();
            broker.CreateQueue("orders");
            await broker.SendAsync("orders", new byte[] { 1 }, null, context.CancellationToken);
            var delivery = (await broker.ReceiveAsync("orders", context.CancellationToken))!;
            await broker.CompleteAsync("orders", delivery, context.CancellationToken);
        }, cancellationToken);
        return result.Trace.Select(entry => entry.Message).ToArray();
    }
}
