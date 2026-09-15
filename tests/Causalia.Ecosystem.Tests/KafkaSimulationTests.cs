using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Kafka.Tests;

[TestClass]
public sealed class KafkaSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UncommittedRecordShouldBeRedeliveredAfterConsumerLeavesGroup()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13002 },
            async context =>
            {
                var cluster = context.CreateKafkaCluster();
                cluster.CreateTopic("orders", 1);
                await cluster.CreateProducer().ProduceAsync("orders", "1", "payload", context.CancellationToken);

                await using (var first = cluster.CreateConsumer("workers", "one"))
                {
                    first.Subscribe("orders");
                    Assert.AreEqual(0L, (await first.ConsumeAsync<string>(context.CancellationToken))!.Offset);
                }

                await using var second = cluster.CreateConsumer("workers", "two");
                second.Subscribe("orders");
                Assert.AreEqual(0L, (await second.ConsumeAsync<string>(context.CancellationToken))!.Offset);
            },
            TestContext.CancellationToken);
    }
}
