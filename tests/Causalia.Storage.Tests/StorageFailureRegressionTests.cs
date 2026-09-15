using Causalia.Faults;
using Causalia.Storage.Exceptions;
using Causalia.Storage.Faults;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Storage.Tests;

[TestClass]
public sealed class StorageFailureRegressionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task FailureShouldDistinguishDurableMutationFromRejection(bool delete, bool afterMutation)
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 14008 },
            async context =>
            {
                var database = context.CreateStorageDatabase("db");
                var observer = database.CreateClient();
                await observer.WriteAsync("key", new byte[] { 1 }, context.CancellationToken);
                var phase = delete
                    ? (afterMutation ? StorageOperationPhase.AfterDelete : StorageOperationPhase.BeforeDelete)
                    : (afterMutation ? StorageOperationPhase.AfterWrite : StorageOperationPhase.BeforeWrite);
                var client = database.CreateClient(options: new SimulationStorageClientOptions
                {
                    Faults = new StorageFaultPlan().Fail(phase, 1, "lost-response")
                });
                async Task MutateAsync()
                {
                    if (delete)
                    {
                        await client.DeleteAsync("key", context.CancellationToken);
                    }
                    else
                    {
                        await client.WriteAsync("key", new byte[] { 2 }, context.CancellationToken);
                    }
                }

                SimulationStorageException? failure = null;
                try
                {
                    await MutateAsync();
                }
                catch (SimulationStorageException exception)
                {
                    failure = exception;
                }

                var expectedType = afterMutation
                    ? typeof(SimulationStorageAmbiguousCommitException)
                    : typeof(SimulationStorageTransientException);
                Assert.AreEqual(expectedType, failure?.GetType());

                var actual = await observer.ReadAsync("key", context.CancellationToken);
                Assert.AreEqual(!(delete && afterMutation), actual.Exists);
                if (actual.Exists)
                {
                    Assert.AreEqual(afterMutation ? 2L : 1L, actual.Version);
                    Assert.AreEqual(afterMutation ? (byte)2 : (byte)1, actual.Value.Span[0]);
                }
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-0.1)]
    [DataRow(1.1)]
    public void InvalidProbabilityShouldBeRejected(double probability)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new StorageFaultPlan().FailAfterCommit(probability));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new StorageFaultPlan().Delay(StorageOperationPhase.BeforeWrite, probability, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProbabilityFaultPolicy<int, int>("invalid", probability, value => value));
    }
}
