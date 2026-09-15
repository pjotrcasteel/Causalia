using Causalia.ExampleApp;
using Causalia.Storage;
using Causalia.Storage.Faults;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Examples.Tests;

[TestClass]
public sealed class OrderServiceSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LostAcknowledgementShouldBeRetriedWithoutDuplicatingOrder()
    {
        var result = await Simulation.RunAsync(new SimulationOptions { Seed = 202601 }, async context =>
        {
            var database = context.CreateStorageDatabase("orders");
            var client = database.CreateClient(options: new SimulationStorageClientOptions
            {
                Faults = new StorageFaultPlan().FailAfterCommit(1)
            });
            var service = new OrderService(new SimulatedOrderStore(client), context.TimeProvider);
            await service.AcceptAsync("order-42", context.CancellationToken);
            await service.AcceptAsync("order-42", context.CancellationToken);
            var actual = await database.CreateClient().ReadAsync("order-42", context.CancellationToken);
            Assert.IsTrue(actual.Exists);
            Assert.AreEqual(1L, actual.Version);
        }, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), result.VirtualElapsed);
        Assert.AreEqual(1, result.Faults.Count);
    }

    [TestMethod]
    public async Task PersistentFailureShouldStopAfterThreeAttempts()
    {
        var result = await Simulation.RunAsync(new SimulationOptions { Seed = 202602 }, async context =>
        {
            var database = context.CreateStorageDatabase("orders");
            var client = database.CreateClient(options: new SimulationStorageClientOptions
            {
                Faults = new StorageFaultPlan().FailBeforeCommit(1)
            });
            var service = new OrderService(new SimulatedOrderStore(client), context.TimeProvider);
            IOException? failure = null;
            try
            {
                await service.AcceptAsync("order-42", context.CancellationToken);
            }
            catch (IOException exception)
            {
                failure = exception;
            }

            Assert.AreEqual(typeof(IOException), failure?.GetType());
            Assert.IsFalse((await database.CreateClient().ReadAsync("order-42", context.CancellationToken)).Exists);
        }, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromMilliseconds(500), result.VirtualElapsed);
        Assert.AreEqual(3, result.Faults.Count);
    }

    [TestMethod]
    public async Task CancellationShouldInterruptRetryDelay()
    {
        var result = await Simulation.RunAsync(new SimulationOptions { Seed = 202603 }, async context =>
        {
            var database = context.CreateStorageDatabase("orders");
            var client = database.CreateClient(options: new SimulationStorageClientOptions
            {
                Faults = new StorageFaultPlan().FailBeforeCommit(1)
            });
            var service = new OrderService(new SimulatedOrderStore(client), context.TimeProvider);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100), context.TimeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, context.CancellationToken);
            OperationCanceledException? failure = null;
            try
            {
                await service.AcceptAsync("order-42", linked.Token);
            }
            catch (OperationCanceledException exception)
            {
                failure = exception;
            }

            Assert.AreEqual(typeof(TaskCanceledException), failure?.GetType());
            Assert.IsFalse((await database.CreateClient().ReadAsync("order-42", context.CancellationToken)).Exists);
        }, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), result.VirtualElapsed);
    }
}
