using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.RabbitMQ.Tests;

[TestClass]
public sealed class RabbitLifecycleRegressionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReceiveAsync_WhenClosedOrCancelledDuringYield_DoesNotLoseMessage(bool cancel)
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateRabbitMqBroker();
            broker.DeclareExchange("orders", RabbitExchangeType.Direct);
            broker.DeclareQueue("orders");
            broker.BindQueue("orders", "orders", "key");
            await broker.PublishAsync("orders", "key", new byte[] { 42 }, context.CancellationToken);
            await using var original = broker.CreateConsumer("orders", "old");
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            var receive = original.ReceiveAsync(cancellation.Token);

            if (cancel)
            {
                cancellation.Cancel();
            }
            else
            {
                await original.DisposeAsync();
            }

            Exception? failure = null;

            try
            {
                await receive;
            }
            catch (Exception exception) when (exception is ObjectDisposedException or OperationCanceledException)
            {
                failure = exception;
            }

            Assert.IsNotNull(failure);
            Assert.AreEqual(0, original.UnacknowledgedCount);
            await using var replacement = broker.CreateConsumer("orders", "new");
            var delivery = await replacement.ReceiveAsync(context.CancellationToken);
            Assert.IsNotNull(delivery);
            CollectionAssert.AreEqual(new byte[] { 42 }, delivery.Body.ToArray());
            await replacement.AckAsync(delivery.DeliveryTag, context.CancellationToken);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task PublishAsync_WhenCancelledDuringYield_DoesNotRouteMessage()
    {
        await Simulation.RunAsync(async context =>
        {
            var broker = context.CreateRabbitMqBroker();
            broker.DeclareExchange("orders", RabbitExchangeType.Direct);
            broker.DeclareQueue("orders");
            broker.BindQueue("orders", "orders", "key");
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            var publish = broker.PublishAsync("orders", "key", new byte[] { 42 }, cancellation.Token);
            cancellation.Cancel();
            var cancelled = false;

            try
            {
                await publish;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
            await using var consumer = broker.CreateConsumer("orders", "reader");
            Assert.IsNull(await consumer.ReceiveAsync(context.CancellationToken));
        }, TestContext.CancellationToken);
    }
}
