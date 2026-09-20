using Causalia.Storage.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Storage.Tests;

[TestClass]
public sealed class StorageVersionAndTransactionRegressionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RecreateDeletedKey_DoesNotReuseVersionOrAcceptStaleCas(bool useTransaction)
    {
        await Simulation.RunAsync(async context =>
        {
            var client = context.CreateStorageDatabase("orders").CreateClient();
            await client.WriteAsync("order", new byte[] { 1 }, context.CancellationToken);
            var stale = await client.ReadAsync("order", context.CancellationToken);
            await client.DeleteAsync("order", context.CancellationToken);
            Assert.AreEqual(0L, (await client.ReadAsync("order", context.CancellationToken)).Version);

            if (useTransaction)
            {
                var transaction = client.BeginTransaction();
                transaction.Write("order", new byte[] { 2 }, expectedVersion: 0);
                await transaction.CommitAsync(context.CancellationToken);
            }
            else
            {
                await client.WriteAsync("order", new byte[] { 2 }, 0, context.CancellationToken);
            }

            var recreated = await client.ReadAsync("order", context.CancellationToken);
            Assert.IsTrue(recreated.Version > stale.Version);
            var rejected = 0;

            try
            {
                await client.WriteAsync("order", new byte[] { 3 }, stale.Version, context.CancellationToken);
            }
            catch (SimulationStorageConcurrencyException)
            {
                rejected++;
            }

            try
            {
                await client.DeleteAsync("order", stale.Version, context.CancellationToken);
            }
            catch (SimulationStorageConcurrencyException)
            {
                rejected++;
            }

            Assert.AreEqual(2, rejected);
            CollectionAssert.AreEqual(new byte[] { 2 }, (await client.ReadAsync("order", context.CancellationToken)).Value.ToArray());
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task CommitAsync_WhileAlreadyCommitting_RejectsConcurrentMutationAndDoubleCommit()
    {
        await Simulation.RunAsync(async context =>
        {
            var client = context.CreateStorageDatabase("orders").CreateClient(options: new SimulationStorageClientOptions
            {
                CommitLatency = TimeSpan.FromSeconds(1)
            });
            var transaction = client.BeginTransaction();
            transaction.Write("order", new byte[] { 1 });
            var firstCommit = transaction.CommitAsync(context.CancellationToken);
            Assert.ThrowsExactly<InvalidOperationException>(() => transaction.Write("order", new byte[] { 2 }));
            Assert.ThrowsExactly<InvalidOperationException>(() => transaction.Delete("order"));
            Assert.ThrowsExactly<InvalidOperationException>(() => transaction.Rollback());
            var rejected = false;

            try
            {
                await transaction.CommitAsync(context.CancellationToken);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Assert.IsTrue(rejected);
            await firstCommit;
            var value = await client.ReadAsync("order", context.CancellationToken);
            Assert.AreEqual(1L, value.Version);
            CollectionAssert.AreEqual(new byte[] { 1 }, value.Value.ToArray());
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task CommitAsync_WhenCancelledBeforeCommit_CanBeRetriedWithoutLosingStagedChanges()
    {
        await Simulation.RunAsync(async context =>
        {
            var client = context.CreateStorageDatabase("orders").CreateClient();
            var transaction = client.BeginTransaction();
            transaction.Write("order", new byte[] { 1 });
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cancellation.Cancel();
            var cancelled = false;

            try
            {
                await transaction.CommitAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
            Assert.IsFalse((await client.ReadAsync("order", context.CancellationToken)).Exists);
            await transaction.CommitAsync(context.CancellationToken);
            Assert.AreEqual(1L, (await client.ReadAsync("order", context.CancellationToken)).Version);
        }, TestContext.CancellationToken);
    }
}
