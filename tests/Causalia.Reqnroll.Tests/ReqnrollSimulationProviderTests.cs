using Causalia;
using Causalia.Exceptions;
using Causalia.Reqnroll;
using Causalia.Linearizability;
using Causalia.Scheduling;

namespace Causalia.Reqnroll.Tests;

[TestClass]
public sealed class ReqnrollSimulationProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Provider_CanBeConstructedAndUsedWithoutReqnrollRuntimeTypes()
    {
        var provider = new ReqnrollSimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 8128 });

        var result = await provider.RunAsync(
            context =>
            {
                Assert.AreEqual(8128UL, context.Seed);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(8128UL, result.Seed);
        Assert.AreSame(result, provider.LastResult);
    }

    [TestMethod]
    public async Task Provider_CanExploreSchedulesWithoutReqnrollRuntimeTypes()
    {
        var provider = new ReqnrollSimulationProvider();
        var result = await provider.ExploreAsync(
            new ExplorationOptions
            {
                Simulation = new SimulationOptions { Seed = 8129 },
                MaxSchedules = 10,
                MaxDecisionDepth = 10
            },
            async context =>
            {
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
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.SchedulesExplored);
        Assert.AreSame(result, provider.LastExplorationResult);
    }

    [TestMethod]
    public async Task Provider_CanUseCoverageGuidedExplorationWithoutReqnrollRuntimeTypes()
    {
        var provider = new ReqnrollSimulationProvider();
        var result = await provider.ExploreAsync(
            new ExplorationOptions
            {
                Strategy = ExplorationStrategy.CoverageGuided,
                Simulation = new SimulationOptions { Seed = 8130 },
                MaxSchedules = 10,
                MaxDecisionDepth = 10
            },
            async context =>
            {
                var value = 0;
                await context.ConcurrentAsync(
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                        value = 1;
                    },
                    async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                        context.Coverage.Observe("value", value);
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(ExplorationStrategy.CoverageGuided, result.Strategy);
        Assert.AreSame(result, provider.LastExplorationResult);
        Assert.IsTrue(result.SchedulesWithNewCoverage >= 2);
    }

    [TestMethod]
    public async Task Provider_RetainsInvariantFailureForLaterReqnrollSteps()
    {
        var provider = new ReqnrollSimulationProvider();

        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                context =>
                {
                    context.Invariants.Always("healthy", () => false);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantViolationException>(provider.LastInvariantFailure);
    }

    [TestMethod]
    public async Task MinimizeAsync_UsesScenarioScopedProviderState()
    {
        var provider = new ReqnrollSimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 881 });
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                _ => throw new InvalidOperationException("failure"),
                TestContext.CancellationToken));

        var result = await provider.MinimizeAsync(
            new Causalia.Minimization.MinimizationOptions { MaxAttempts = 10 },
            failure,
            _ => throw new InvalidOperationException("failure"),
            TestContext.CancellationToken);

        Assert.AreSame(result, provider.LastMinimizationResult);
        Assert.AreSame(result.MinimizedFailure, provider.LastFailure);
    }

    [TestMethod]
    public async Task Provider_RetainsLinearizabilityFailureForLaterReqnrollSteps()
    {
        var provider = new ReqnrollSimulationProvider();

        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                context =>
                {
                    var history = context.Linearizability.CreateHistory<string, int>("reqnroll");
                    history.Begin("client", "read").Complete(1);
                    var specification = new LinearizabilitySpecification<int, string, int>(
                        0,
                        (state, _, output) => output == state
                            ? LinearizabilityStep<int>.Accept(state)
                            : LinearizabilityStep<int>.Reject(state));
                    history.RequireLinearizable(specification);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationLinearizabilityViolationException>(provider.LastLinearizabilityFailure);
    }

}
