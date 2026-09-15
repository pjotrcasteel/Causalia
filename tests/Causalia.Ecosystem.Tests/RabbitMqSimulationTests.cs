using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.RabbitMQ.Tests;

[TestClass]
public sealed class RabbitMqSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ClosingConsumerShouldRequeueUnackedDelivery()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13003 },
            async context =>
            {
                var broker = context.CreateRabbitMqBroker();
                broker.DeclareExchange("events", RabbitExchangeType.Fanout);
                broker.DeclareQueue("orders");
                broker.BindQueue("events", "orders");
                await broker.PublishAsync("events", string.Empty, new byte[] { 1 }, context.CancellationToken);

                await using (var first = broker.CreateConsumer("orders", "one"))
                {
                    Assert.IsFalse((await first.ReceiveAsync(context.CancellationToken))!.Redelivered);
                }

                await using var second = broker.CreateConsumer("orders", "two");
                Assert.IsTrue((await second.ReceiveAsync(context.CancellationToken))!.Redelivered);
            },
            TestContext.CancellationToken);
    }
}
