using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Kafka.Tests;

[TestClass]
public sealed class KafkaLifecycleRegressionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommitAsync_WhenRebalancedDuringDelay_RejectsStaleMember(bool reuseMemberId)
    {
        await Simulation.RunAsync(async context =>
        {
            var cluster = context.CreateKafkaCluster(faults: new KafkaFaultPlan().DelayCommit(1, TimeSpan.FromSeconds(10)));
            cluster.CreateTopic("orders", 1);
            await using var original = cluster.CreateConsumer("workers", "z-old");
            original.Subscribe("orders");
            await cluster.CreateProducer().ProduceAsync("orders", "key", "order", context.CancellationToken);
            var record = await original.ConsumeAsync<string>(context.CancellationToken);
            Assert.IsNotNull(record);
            var commit = original.CommitAsync(record, context.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);

            if (reuseMemberId)
            {
                await original.DisposeAsync();
            }

            await using var replacement = cluster.CreateConsumer("workers", reuseMemberId ? "z-old" : "a-new");
            replacement.Subscribe("orders");
            var rejected = false;

            try
            {
                await commit;
            }
            catch (SimulationKafkaPartitionOwnershipException)
            {
                rejected = true;
            }

            Assert.IsTrue(rejected);
            await replacement.DisposeAsync();
            await original.DisposeAsync();
            await using var verifier = cluster.CreateConsumer("workers", "verifier");
            verifier.Subscribe("orders");
            Assert.AreEqual(0L, (await verifier.ConsumeAsync<string>(context.CancellationToken))!.Offset);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ConsumeAsync_WhenMemberIsReplacedDuringYield_DoesNotConsumeForNewMember()
    {
        await Simulation.RunAsync(async context =>
        {
            var cluster = context.CreateKafkaCluster();
            cluster.CreateTopic("orders", 1);
            await cluster.CreateProducer().ProduceAsync("orders", "key", "order", context.CancellationToken);
            var original = cluster.CreateConsumer("workers", "same-id");
            original.Subscribe("orders");
            var consume = original.ConsumeAsync<string>(context.CancellationToken);
            await original.DisposeAsync();
            await using var replacement = cluster.CreateConsumer("workers", "same-id");
            replacement.Subscribe("orders");
            var rejected = false;

            try
            {
                await consume;
            }
            catch (ObjectDisposedException)
            {
                rejected = true;
            }

            Assert.IsTrue(rejected);
            Assert.AreEqual(0L, (await replacement.ConsumeAsync<string>(context.CancellationToken))!.Offset);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task CommitStoredOffsetsAsync_WhenNewOffsetStoredDuringCommit_PreservesNewOffset()
    {
        await Simulation.RunAsync(async context =>
        {
            var cluster = context.CreateKafkaCluster(faults: new KafkaFaultPlan().DelayCommit(1, TimeSpan.FromSeconds(2)));
            cluster.CreateTopic("orders", 1);
            var producer = cluster.CreateProducer();
            await producer.ProduceAsync("orders", "key", "first", context.CancellationToken);
            await producer.ProduceAsync("orders", "key", "second", context.CancellationToken);
            var consumer = cluster.CreateConsumer("workers", "one");
            consumer.Subscribe("orders");
            var first = await consumer.ConsumeAsync<string>(context.CancellationToken);
            var second = await consumer.ConsumeAsync<string>(context.CancellationToken);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            consumer.StoreOffset(first);
            var commit = consumer.CommitStoredOffsetsAsync(context.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
            consumer.StoreOffset(second);
            await commit;
            await consumer.CommitStoredOffsetsAsync(context.CancellationToken);
            await consumer.DisposeAsync();
            await using var verifier = cluster.CreateConsumer("workers", "two");
            verifier.Subscribe("orders");
            Assert.IsNull(await verifier.ConsumeAsync<string>(context.CancellationToken));
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ProduceAsync_WhenCancelledDuringYield_DoesNotAppendRecord()
    {
        await Simulation.RunAsync(async context =>
        {
            var cluster = context.CreateKafkaCluster();
            cluster.CreateTopic("orders", 1);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            var produce = cluster.CreateProducer().ProduceAsync("orders", "key", "order", cancellation.Token);
            cancellation.Cancel();
            var cancelled = false;

            try
            {
                await produce;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
            await using var consumer = cluster.CreateConsumer("workers", "one");
            consumer.Subscribe("orders");
            Assert.IsNull(await consumer.ConsumeAsync<string>(context.CancellationToken));
        }, TestContext.CancellationToken);
    }
}
