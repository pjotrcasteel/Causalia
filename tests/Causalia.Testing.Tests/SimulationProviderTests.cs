using Causalia;
using Causalia.Consistency;
using Causalia.Exceptions;
using Causalia.Testing;
using Causalia.Linearizability;
using Causalia.ModelBased;
using Causalia.Scheduling;

namespace Causalia.Testing.Tests;

[TestClass]
public sealed class SimulationProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_UsesConfiguredOptionsAndRetainsLastResult()
    {
        var provider = new SimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 741 });

        var result = await provider.RunAsync(
            context =>
            {
                Assert.AreEqual(741UL, context.Seed);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreSame(result, provider.LastResult);
        Assert.IsNull(provider.LastFailure);
        Assert.AreEqual(741UL, provider.Options.Seed);
    }

    [TestMethod]
    public async Task RunAsync_WhenSimulationFails_RetainsLastFailureAndClearsLastResult()
    {
        var provider = new SimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 742 });

        var exception = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                _ => throw new InvalidOperationException("failure"),
                TestContext.CancellationToken));

        Assert.AreSame(exception, provider.LastFailure);
        Assert.IsNull(provider.LastResult);
    }

    [TestMethod]
    public async Task ExploreAsync_WhenFailureIsDiscovered_RetainsExplorationAndSimulationFailure()
    {
        var provider = new SimulationProvider();
        var options = new SimulationOptions { Seed = 743 };

        var exception = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await provider.ExploreAsync(
                new ExplorationOptions
                {
                    Simulation = options,
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

                            if (value == 0)
                            {
                                throw new InvalidOperationException("race");
                            }
                        },
                        context.CancellationToken);
                },
                TestContext.CancellationToken));

        Assert.AreSame(exception, provider.LastExplorationFailure);
        Assert.IsNotNull(provider.LastFailure);
        Assert.IsNull(provider.LastExplorationResult);
    }

    [TestMethod]
    public async Task ReplayAsync_UsesConfiguredOptionsAndRetainsLastResult()
    {
        var provider = new SimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 744 });
        var first = await provider.RunAsync(
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

        var replay = await provider.ReplayAsync(
            first.Schedule,
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

        Assert.AreSame(replay, provider.LastResult);
        Assert.AreEqual(first.Schedule.ReplayToken, replay.Schedule.ReplayToken);
    }

    [TestMethod]
    public async Task RunAsync_WhenInvariantFails_RetainsInvariantFailure()
    {
        var provider = new SimulationProvider();

        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                context =>
                {
                    context.Invariants.Never("forbidden", () => true);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationInvariantViolationException>(provider.LastInvariantFailure);
        Assert.IsNotNull(provider.LastFailure);
    }

    [TestMethod]
    public async Task MinimizeAsync_RetainsMinimizationResultAndMinimizedFailure()
    {
        var provider = new SimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 749 });
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
    public async Task ReproduceAsync_WithMinimizedReproduction_RetainsReproducedFailure()
    {
        var provider = new SimulationProvider();
        provider.Configure(new SimulationOptions { Seed = 750 });
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                _ => throw new InvalidOperationException("failure"),
                TestContext.CancellationToken));
        var minimized = await provider.MinimizeAsync(
            new Causalia.Minimization.MinimizationOptions { MaxAttempts = 10 },
            failure,
            _ => throw new InvalidOperationException("failure"),
            TestContext.CancellationToken);

        var replayFailure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.ReproduceAsync(
                minimized.Reproduction,
                _ => throw new InvalidOperationException("failure"),
                TestContext.CancellationToken));

        Assert.AreSame(replayFailure, provider.LastFailure);
    }

    [TestMethod]
    public async Task RunAsync_WhenConsistencyFails_RetainsConsistencyFailure()
    {
        var provider = new SimulationProvider();

        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                context =>
                {
                    var history = context.Consistency.CreateHistory<string>("provider")
                        .Require(ConsistencyGuarantees.ReadYourWrites);
                    history.Write("session", "primary", "key");
                    history.Read("session", "secondary", "key", null);
                    return Task.CompletedTask;
                },
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<SimulationConsistencyViolationException>(provider.LastConsistencyFailure);
        Assert.IsNotNull(provider.LastFailure);
    }

    [TestMethod]
    public async Task RunAsync_WhenLinearizabilityFails_RetainsLinearizabilityFailure()
    {
        var provider = new SimulationProvider();

        await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await provider.RunAsync(
                context =>
                {
                    var history = context.Linearizability.CreateHistory<string, int>("provider");
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
        Assert.IsNotNull(provider.LastFailure);
    }

    [TestMethod]
    public async Task CheckModelAsync_WhenModelFails_RetainsModelExplorationAndSimulationFailure()
    {
        var provider = new SimulationProvider();
        var specification = new ModelBasedSpecification<int, object>(
            "provider-model",
            static () => 0,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new object());
            },
            state =>
            [
                ModelCommand.Create<int, object, int>(
                    "fail",
                    current => current + 1,
                    static (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(2);
                    },
                    static (_, expected, observed) => ModelCommandVerification.Equal(expected, observed),
                    current => current == 0)
            ]);

        var exception = await Assert.ThrowsExactlyAsync<SimulationModelExplorationFailedException>(async () =>
            await provider.CheckModelAsync(
                new ModelBasedOptions
                {
                    Simulation = new SimulationOptions { Seed = 751 }
                },
                specification,
                TestContext.CancellationToken));

        Assert.AreSame(exception, provider.LastModelBasedFailure);
        Assert.IsNull(provider.LastModelBasedResult);
        Assert.IsNotNull(provider.LastFailure);
        Assert.IsInstanceOfType<SimulationModelViolationException>(provider.LastModelFailure);
    }

}
