using Causalia.Exceptions;
using Causalia.Storage;
using Causalia.Storage.Exceptions;
using Causalia.Storage.Faults;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.EntityFrameworkCore.Tests;

[TestClass]
public sealed class CausaliaSaveChangesInterceptorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SaveChangesAsyncShouldUseVirtualCommitLatency()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 14101 },
            async context =>
            {
                var boundary = context.CreateStorageBoundary(
                    "ef-db",
                    options: new SimulationStorageClientOptions { CommitLatency = TimeSpan.FromHours(3) });
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseInMemoryDatabase($"ef-{context.Seed}")
                    .AddCausaliaStorageSimulation(boundary)
                    .Options;
                await using var dbContext = new TestDbContext(options);
                dbContext.Items.Add(new TestItem { Id = 1, Name = "one" });
                await dbContext.SaveChangesAsync(context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromHours(3), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task AfterCommitFailureShouldBeAmbiguousWhileDataRemainsCommitted()
    {
        TestDbContext? dbContext = null;
        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 14102 },
                async context =>
                {
                    var faults = new StorageFaultPlan().FailAfterCommit(1);
                    var boundary = context.CreateStorageBoundary(
                        "ef-db",
                        options: new SimulationStorageClientOptions { Faults = faults });
                    var options = new DbContextOptionsBuilder<TestDbContext>()
                        .UseInMemoryDatabase($"ef-{context.Seed}")
                        .AddCausaliaStorageSimulation(boundary)
                        .Options;
                    dbContext = new TestDbContext(options);
                    dbContext.Items.Add(new TestItem { Id = 1, Name = "one" });
                    await dbContext.SaveChangesAsync(context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.IsNotNull(dbContext);
        Assert.AreEqual(1, dbContext.Items.Count());
        await dbContext.DisposeAsync();
    }

    [TestMethod]
    public async Task SynchronousSaveChangesShouldBeRejected()
    {
        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 14103 },
                context =>
                {
                    var boundary = context.CreateStorageBoundary("ef-db");
                    var options = new DbContextOptionsBuilder<TestDbContext>()
                        .UseInMemoryDatabase($"ef-{context.Seed}")
                        .AddCausaliaStorageSimulation(boundary)
                        .Options;
                    using var dbContext = new TestDbContext(options);
                    dbContext.Items.Add(new TestItem { Id = 1, Name = "one" });
                    dbContext.SaveChanges();
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
            : base(options)
        {
        }

        public DbSet<TestItem> Items => Set<TestItem>();
    }

    private sealed class TestItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
