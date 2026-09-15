using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Dapr.Tests;

[TestClass]
public sealed class DaprSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StateEtagsAndPubSubRedeliveryShouldBeDeterministic()
    {
        var deliveries = 0;
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 13001 },
            async context =>
            {
                var environment = context.CreateDaprEnvironment();
                environment.AddStateStore("state");
                var pubSub = environment.AddPubSub("events");
                var client = environment.CreateClient("orders");
                await client.SaveStateAsync("state", "counter", 1, context.CancellationToken);
                var first = await client.GetStateAsync<int>("state", "counter", context.CancellationToken);
                await client.SaveStateAsync("state", "counter", 2, context.CancellationToken);
                Assert.IsFalse(await client.TrySaveStateAsync("state", "counter", 3, first.ETag!, context.CancellationToken));
                using var subscription = pubSub.Subscribe<string>(
                    "orders",
                    "worker",
                    (message, _) =>
                    {
                        deliveries++;
                        return Task.FromResult(message.Attempt == 1 ? DaprPubSubResult.Retry : DaprPubSubResult.Success);
                    },
                    new DaprSubscriptionOptions { RetryDelay = TimeSpan.FromSeconds(2) });
                await client.PublishEventAsync("events", "orders", "payload", context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(2, deliveries);
        Assert.AreEqual(TimeSpan.FromSeconds(2), result.VirtualElapsed);
    }
}
