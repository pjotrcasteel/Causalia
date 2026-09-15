using System.Text;
using Causalia.Exceptions;
using Causalia.Storage.Exceptions;
using Causalia.Storage.Faults;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Storage.Tests;

[TestClass]
public sealed class SimulationStorageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CommittedDataShouldSurviveNodeRestart()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 14001 },
            async context =>
            {
                var node = context.CreateNode("orders");
                var database = context.CreateStorageDatabase("orders-db");
                var client = database.CreateClient(node);
                await client.WriteAsync("order/42", Encoding.UTF8.GetBytes("created"), context.CancellationToken);
                node.Crash();
                node.Restart();
                var result = await client.ReadAsync("order/42", context.CancellationToken);
                Assert.IsTrue(result.Exists);
                Assert.AreEqual("created", Encoding.UTF8.GetString(result.Value.Span));
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task TransactionShouldCommitAtomically()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 14002 },
            async context =>
            {
                var database = context.CreateStorageDatabase("db");
                var client = database.CreateClient();
                var transaction = client.BeginTransaction();
                transaction.Write("a", new byte[] { 1 });
                transaction.Write("b", new byte[] { 2 });
                await transaction.CommitAsync(context.CancellationToken);
                Assert.IsTrue((await client.ReadAsync("a", context.CancellationToken)).Exists);
                Assert.IsTrue((await client.ReadAsync("b", context.CancellationToken)).Exists);
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task AfterCommitFailureShouldLeaveDurableDataCommitted()
    {
        SimulationStorageDatabase? database = null;
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 14003 },
                async context =>
                {
                    database = context.CreateStorageDatabase("db");
                    var faults = new StorageFaultPlan().FailAfterCommit(1);
                    var client = database.CreateClient(options: new SimulationStorageClientOptions { Faults = faults });
                    var transaction = client.BeginTransaction();
                    transaction.Write("order/42", new byte[] { 1 });
                    await transaction.CommitAsync(context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationStorageAmbiguousCommitException>(failure.InnerException);
        Assert.IsNotNull(database);
        var read = await database.CreateClient().ReadAsync("order/42", TestContext.CancellationToken);
        Assert.IsTrue(read.Exists);
    }


    [TestMethod]
    public async Task BeforeCommitFailureShouldLeaveDurableDataUnchanged()
    {
        SimulationStorageDatabase? database = null;
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 140031 },
                async context =>
                {
                    database = context.CreateStorageDatabase("db");
                    var faults = new StorageFaultPlan().FailBeforeCommit(1);
                    var client = database.CreateClient(options: new SimulationStorageClientOptions { Faults = faults });
                    var transaction = client.BeginTransaction();
                    transaction.Write("order/42", new byte[] { 1 });
                    await transaction.CommitAsync(context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationStorageTransientException>(failure.InnerException);
        Assert.IsNotNull(database);
        var read = await database.CreateClient().ReadAsync("order/42", TestContext.CancellationToken);
        Assert.IsFalse(read.Exists);
    }

    [TestMethod]
    public async Task ExpectedVersionShouldDetectLostUpdate()
    {
        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 14004 },
                async context =>
                {
                    var database = context.CreateStorageDatabase("db");
                    var client = database.CreateClient();
                    await client.WriteAsync("counter", new byte[] { 0 }, context.CancellationToken);
                    var original = await client.ReadAsync("counter", context.CancellationToken);
                    await client.WriteAsync("counter", new byte[] { 1 }, original.Version, context.CancellationToken);
                    await client.WriteAsync("counter", new byte[] { 2 }, original.Version, context.CancellationToken);
                },
                TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task StorageLatencyShouldAdvanceVirtualTime()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 14005 },
            async context =>
            {
                var database = context.CreateStorageDatabase("db");
                var client = database.CreateClient(
                    options: new SimulationStorageClientOptions { OperationLatency = TimeSpan.FromHours(4) });
                await client.WriteAsync("x", new byte[] { 1 }, context.CancellationToken);
                _ = await client.ReadAsync("x", context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromHours(8), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task OperationLeaseShouldRejectASecondTerminalTransition()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 14006 },
            async context =>
            {
                var boundary = context.CreateStorageBoundary("db");
                var lease = await boundary.BeginAsync(StorageOperationKind.Write, "order/42", context.CancellationToken);
                await boundary.CompleteAsync(lease, context.CancellationToken);

                Assert.ThrowsExactly<InvalidOperationException>(
                    () => boundary.ProviderFailed(lease, new InvalidOperationException("provider failed")));
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ProviderCanceledShouldCreateTerminalCancellationTrace()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 14007 },
            async context =>
            {
                var boundary = context.CreateStorageBoundary("db");
                var lease = await boundary.BeginAsync(StorageOperationKind.Commit, null, context.CancellationToken);
                boundary.ProviderCanceled(lease, context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.Trace.Any(entry => entry.Message.Contains("provider-cancelled", StringComparison.Ordinal)));
    }
}
