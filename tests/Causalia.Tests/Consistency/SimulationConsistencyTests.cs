using Causalia.Consistency;
using Causalia.Exceptions;
using Causalia.Scheduling;

namespace Causalia.Tests.Consistency;

[TestClass]
public sealed class SimulationConsistencyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Read_WhenSessionDoesNotObserveOwnWrite_ReportsReadYourWritesViolation()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1501 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("orders")
                        .Require(ConsistencyGuarantees.ReadYourWrites);
                    history.Write("checkout", "primary", "order:42");
                    history.Read("checkout", "secondary", "order:42", null);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        var violation = GetConsistencyViolation(failure);
        Assert.AreEqual(ConsistencyViolationKind.ReadYourWrites, violation.Kind);
        Assert.AreEqual("checkout", violation.SessionId);
        Assert.AreEqual("order:42", violation.Key);
    }

    [TestMethod]
    public async Task Read_WhenSessionObservesDescendantOfOwnWrite_SatisfiesReadYourWrites()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1502 },
            context =>
            {
                var history = context.Consistency.CreateHistory<string>("orders")
                    .Require(ConsistencyGuarantees.ReadYourWrites);
                history.Write("checkout", "primary", "order:42");
                var updated = history.Write("checkout", "primary", "order:42");
                history.Read("checkout", "secondary", "order:42", updated);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.Consistency.Count);
        Assert.AreEqual(2, result.Consistency[0].Writes);
        Assert.AreEqual(1, result.Consistency[0].Reads);
    }

    [TestMethod]
    public async Task Read_WhenSessionRegressesToOlderVersion_ReportsMonotonicReadsViolation()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1503 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("catalog")
                        .Require(ConsistencyGuarantees.MonotonicReads);
                    var first = history.Write("publisher", "primary", "product:7");
                    var second = history.Write("publisher", "primary", "product:7");
                    history.Read("browser", "replica-a", "product:7", second);
                    history.Read("browser", "replica-b", "product:7", first);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        var violation = GetConsistencyViolation(failure);
        Assert.AreEqual(ConsistencyViolationKind.MonotonicReads, violation.Kind);
        Assert.AreEqual("browser", violation.SessionId);
    }

    [TestMethod]
    public async Task ObserveReplica_WhenLaterSessionWriteIsVisibleWithoutEarlierWrite_ReportsMonotonicWritesViolation()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1504 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("profile")
                        .Require(ConsistencyGuarantees.MonotonicWrites);
                    history.Write("editor", "primary", "name");
                    var avatar = history.Write("editor", "primary", "avatar");
                    history.ObserveReplica(
                        "secondary",
                        new Dictionary<string, ConsistencyVersion<string>?> { ["avatar"] = avatar });
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        var violation = GetConsistencyViolation(failure);
        Assert.AreEqual(ConsistencyViolationKind.MonotonicWrites, violation.Kind);
        Assert.AreEqual("secondary", violation.ReplicaId);
        Assert.AreEqual("name", violation.Key);
    }

    [TestMethod]
    public async Task ObserveReplica_WhenWriteIsVisibleWithoutPreviouslyReadVersion_ReportsWritesFollowReadsViolation()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1505 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("checkout")
                        .Require(ConsistencyGuarantees.WritesFollowReads);
                    var customer = history.Write("crm", "primary", "customer:42");
                    history.Read("checkout", "primary", "customer:42", customer);
                    var order = history.Write("checkout", "primary", "order:99");
                    history.ObserveReplica(
                        "secondary",
                        new Dictionary<string, ConsistencyVersion<string>?> { ["order:99"] = order });
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        var violation = GetConsistencyViolation(failure);
        Assert.AreEqual(ConsistencyViolationKind.WritesFollowReads, violation.Kind);
        Assert.AreEqual("customer:42", violation.Key);
    }

    [TestMethod]
    public async Task ObserveReplica_WhenCausalPredecessorIsMissing_ReportsCausalVisibilityViolation()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1506 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("fulfilment")
                        .Require(ConsistencyGuarantees.CausalVisibility);
                    var order = history.Write("orders", "primary", "order:42");
                    history.Read("fulfilment", "primary", "order:42", order);
                    var plan = history.Write("fulfilment", "primary", "plan:42");
                    history.ObserveReplica(
                        "secondary",
                        new Dictionary<string, ConsistencyVersion<string>?> { ["plan:42"] = plan });
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        var violation = GetConsistencyViolation(failure);
        Assert.AreEqual(ConsistencyViolationKind.CausalVisibility, violation.Kind);
        Assert.AreEqual("order:42", violation.Key);
    }

    [TestMethod]
    public async Task ObserveReplica_WhenCausalPredecessorIsPresent_SatisfiesCausalVisibility()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1507 },
            context =>
            {
                var history = context.Consistency.CreateHistory<string>("fulfilment")
                    .Require(ConsistencyGuarantees.CausalVisibility);
                var order = history.Write("orders", "primary", "order:42");
                history.Read("fulfilment", "primary", "order:42", order);
                var plan = history.Write("fulfilment", "primary", "plan:42");
                history.ObserveReplica(
                    "secondary",
                    new Dictionary<string, ConsistencyVersion<string>?>
                    {
                        ["order:42"] = order,
                        ["plan:42"] = plan
                    });
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.Consistency[0].ReplicaObservations);
    }

    [TestMethod]
    public async Task Read_WhenReplicaSnapshotDisagrees_ReportsReplicaReadAgreementViolation()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1508 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("inventory")
                        .Require(ConsistencyGuarantees.ReplicaReadAgreement);
                    var version = history.Write("writer", "primary", "stock:42");
                    history.ObserveReplica(
                        "secondary",
                        new Dictionary<string, ConsistencyVersion<string>?> { ["stock:42"] = version });
                    history.Read("reader", "secondary", "stock:42", null);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        var violation = GetConsistencyViolation(failure);
        Assert.AreEqual(ConsistencyViolationKind.ReplicaReadAgreement, violation.Kind);
        Assert.AreEqual("secondary", violation.ReplicaId);
    }

    [TestMethod]
    public async Task RequireConvergence_WhenReplicaCatchesUp_CompletesAtVirtualTime()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1509 },
            async context =>
            {
                var history = context.Consistency.CreateHistory<string>("inventory");
                var version = history.Write("writer", "primary", "stock:42");
                history.ObserveReplica(
                    "primary",
                    new Dictionary<string, ConsistencyVersion<string>?> { ["stock:42"] = version });
                history.ObserveReplica("secondary", new Dictionary<string, ConsistencyVersion<string>?>());
                history.RequireConvergence("inventory converges", TimeSpan.FromSeconds(10), ["primary", "secondary"]);

                await Task.Delay(TimeSpan.FromSeconds(3), context.TimeProvider, context.CancellationToken);
                history.ObserveReplica(
                    "secondary",
                    new Dictionary<string, ConsistencyVersion<string>?> { ["stock:42"] = version });
            },
            TestContext.CancellationToken);

        var convergence = result.Consistency[0].Convergence.Single();
        Assert.AreEqual(TimeSpan.FromSeconds(3), convergence.CompletedAt - convergence.RegisteredAt);
        Assert.AreEqual(TimeSpan.FromSeconds(3), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task RequireConvergence_WhenReplicasStayDifferent_FailsAtVirtualDeadline()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1510 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("inventory");
                    var version = history.Write("writer", "primary", "stock:42");
                    history.ObserveReplica(
                        "primary",
                        new Dictionary<string, ConsistencyVersion<string>?> { ["stock:42"] = version });
                    history.ObserveReplica("secondary", new Dictionary<string, ConsistencyVersion<string>?>());
                    history.RequireConvergence("inventory converges", TimeSpan.FromSeconds(5), ["primary", "secondary"]);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        var violation = GetConsistencyViolation(failure);
        Assert.AreEqual(ConsistencyViolationKind.Convergence, violation.Kind);
        Assert.AreEqual(TimeSpan.FromSeconds(5), failure.Trace[^1].Timestamp - failure.Trace[0].Timestamp);
    }

    [TestMethod]
    public async Task RequireConvergence_WhenMissingAndExplicitNullRepresentNoValue_CompletesImmediately()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1511 },
            context =>
            {
                var history = context.Consistency.CreateHistory<string>("inventory");
                history.ObserveReplica("primary", new Dictionary<string, ConsistencyVersion<string>?>());
                history.ObserveReplica(
                    "secondary",
                    new Dictionary<string, ConsistencyVersion<string>?> { ["stock:42"] = null });
                history.RequireConvergence("empty replicas agree", TimeSpan.FromSeconds(5), ["primary", "secondary"]);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.Zero, result.VirtualElapsed);
        Assert.AreEqual(TimeSpan.Zero, result.Consistency.Single().Convergence.Single().CompletedAt - result.Consistency.Single().Convergence.Single().RegisteredAt);
    }

    [TestMethod]
    public async Task RequireConvergence_WhenZeroDurationStartsDiverged_FailsImmediately()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1512 },
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("inventory");
                    var version = history.Write("writer", "primary", "stock:42");
                    history.ObserveReplica(
                        "primary",
                        new Dictionary<string, ConsistencyVersion<string>?> { ["stock:42"] = version });
                    history.ObserveReplica("secondary", new Dictionary<string, ConsistencyVersion<string>?>());
                    history.RequireConvergence("already converged", TimeSpan.Zero, ["primary", "secondary"]);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.AreEqual(ConsistencyViolationKind.Convergence, GetConsistencyViolation(failure).Kind);
        Assert.IsTrue(failure.Trace.All(entry => entry.Timestamp == failure.Trace[0].Timestamp));
    }

    [TestMethod]
    public async Task RunAsync_WithConsistencyHistory_ReturnsStableOutcome()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1513 },
            context =>
            {
                var history = context.Consistency.CreateHistory<string>("orders")
                    .Require(ConsistencyGuarantees.Session | ConsistencyGuarantees.ReplicaReadAgreement);
                var version = history.Write("checkout", "primary", "order:42");
                history.ObserveReplica(
                    "primary",
                    new Dictionary<string, ConsistencyVersion<string>?> { ["order:42"] = version });
                history.Read("checkout", "primary", "order:42", version);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        var outcome = result.Consistency.Single();
        Assert.AreEqual("orders", outcome.HistoryName);
        Assert.AreEqual(ConsistencyGuarantees.Session | ConsistencyGuarantees.ReplicaReadAgreement, outcome.Guarantees);
        Assert.AreEqual(1, outcome.Writes);
        Assert.AreEqual(1, outcome.Reads);
        Assert.AreEqual(1, outcome.ReplicaObservations);
    }

    [TestMethod]
    public async Task ExploreAsync_WithDporAndReplicaRace_DiscoversConsistencyViolation()
    {
        var explorationFailure = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
                    Simulation = new SimulationOptions { Seed = 1514 },
                    MaxSchedules = 10,
                    MaxDecisionDepth = 20
                },
                async context =>
                {
                    var history = context.Consistency.CreateHistory<string>("inventory")
                        .Require(ConsistencyGuarantees.ReplicaReadAgreement);
                    var version = history.Write("writer", "primary", "stock:42");
                    history.ObserveReplica("secondary", new Dictionary<string, ConsistencyVersion<string>?>());

                    await Task.WhenAll(
                        context.Exploration.RunAsync(
                            "replicate",
                            [context.Exploration.Write("secondary:stock:42")],
                            async cancellationToken =>
                            {
                                await Task.Yield();
                                cancellationToken.ThrowIfCancellationRequested();
                                history.ObserveReplica(
                                    "secondary",
                                    new Dictionary<string, ConsistencyVersion<string>?> { ["stock:42"] = version });
                            },
                            context.CancellationToken),
                        context.Exploration.RunAsync(
                            "read",
                            [context.Exploration.Read("secondary:stock:42")],
                            async cancellationToken =>
                            {
                                await Task.Yield();
                                cancellationToken.ThrowIfCancellationRequested();
                                history.Read("reader", "secondary", "stock:42", version);
                            },
                            context.CancellationToken));
                },
                TestContext.CancellationToken));

        Assert.AreEqual(ConsistencyViolationKind.ReplicaReadAgreement, explorationFailure.Failure.ConsistencyFailure?.Kind);
    }

    [TestMethod]
    public async Task ReplayAsync_WhenConsistencyViolationWasCaptured_ReproducesExactFailure()
    {
        var options = new SimulationOptions { Seed = 1515 };
        var discovered = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(options, ConsistencyFailureScenarioAsync, TestContext.CancellationToken));

        var replayed = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReplayAsync(
                options,
                discovered.Schedule,
                ConsistencyFailureScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual(discovered.Schedule.ReplayToken, replayed.Schedule.ReplayToken);
        Assert.AreEqual(ConsistencyViolationKind.ReadYourWrites, GetConsistencyViolation(replayed).Kind);
    }

    [DeterministicSimulation]
    private static async Task ConsistencyFailureScenarioAsync(SimulationContext context)
    {
        var history = context.Consistency.CreateHistory<string>("orders")
            .Require(ConsistencyGuarantees.ReadYourWrites);
        history.Write("checkout", "primary", "order:42");

        await context.ConcurrentAsync(
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            },
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                history.Read("checkout", "secondary", "order:42", null);
            },
            context.CancellationToken);
    }

    private static SimulationConsistencyViolationException GetConsistencyViolation(SimulationFailedException failure)
    {
        Assert.AreEqual(typeof(SimulationConsistencyViolationException), failure.InnerException?.GetType());
        return (SimulationConsistencyViolationException)failure.InnerException!;
    }
}
