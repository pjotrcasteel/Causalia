using Causalia.Exceptions;
using Causalia.Messaging;
using Causalia.Messaging.Faults;
using Causalia.Minimization;
using Causalia.Scheduling;

namespace Causalia.Tests.Minimization;

[TestClass]
public sealed class SimulationMinimizationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MinimizeAsync_WhenOneOfTwoFaultsIsRequired_RemovesUnnecessaryFault()
    {
        var options = new SimulationOptions { Seed = 9001 };
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(options, FaultScenarioAsync, TestContext.CancellationToken));

        Assert.AreEqual(2, failure.Faults.Count);

        var result = await Simulation.MinimizeAsync(
            options,
            new MinimizationOptions { MaxAttempts = 100 },
            failure,
            FaultScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.OriginalFaultCount);
        Assert.AreEqual(1, result.EssentialFaultCount);
        Assert.AreEqual("messaging.drop", result.Reproduction.Faults[0].PolicyName);
    }

    [TestMethod]
    public async Task ReproduceAsync_WithMinimizedToken_ReproducesSameInvariantFailure()
    {
        var options = new SimulationOptions { Seed = 9002 };
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(options, FaultScenarioAsync, TestContext.CancellationToken));
        var result = await Simulation.MinimizeAsync(
            options,
            new MinimizationOptions { MaxAttempts = 100 },
            failure,
            FaultScenarioAsync,
            TestContext.CancellationToken);
        var reproduction = SimulationReproduction.Parse(result.Reproduction.ReplayToken);

        var replayFailure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReproduceAsync(options, reproduction, FaultScenarioAsync, TestContext.CancellationToken));

        Assert.AreEqual("message is eventually delivered", replayFailure.InvariantFailure?.InvariantName);
        Assert.AreEqual(result.Reproduction.ReplayToken, reproduction.ReplayToken);
    }

    [TestMethod]
    public async Task MinimizeAsync_WhenExplorationFindsRace_ProducesReplayableSchedulerReproduction()
    {
        var options = new SimulationOptions { Seed = 9003 };
        var explorationFailure = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Simulation = options,
                    MaxSchedules = 20,
                    MaxDecisionDepth = 20
                },
                RaceScenarioAsync,
                TestContext.CancellationToken));

        var result = await Simulation.MinimizeAsync(
            options,
            new MinimizationOptions { MaxAttempts = 100 },
            explorationFailure.Failure,
            RaceScenarioAsync,
            TestContext.CancellationToken);

        Assert.IsTrue(result.EssentialSchedulerChoiceCount <= result.OriginalNonCanonicalSchedulerChoiceCount);

        var replayFailure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.ReproduceAsync(options, result.Reproduction, RaceScenarioAsync, TestContext.CancellationToken));

        Assert.AreEqual("reader observed stale state", replayFailure.InnerException?.Message);
    }

    [TestMethod]
    public void SimulationReproduction_Parse_RoundTripsSchedulerChoicesAndFaults()
    {
        var reproduction = new SimulationReproduction(
            9004,
            new[] { new SchedulerChoice(3, 2) },
            new[] { new Causalia.Faults.FaultOccurrence("storage", 1, 7, "storage.timeout") });

        var parsed = SimulationReproduction.Parse(reproduction.ReplayToken);

        Assert.AreEqual(reproduction.ReplayToken, parsed.ReplayToken);
        Assert.AreEqual(9004UL, parsed.Seed);
        Assert.AreEqual(1, parsed.SchedulerChoices.Count);
        Assert.AreEqual(1, parsed.Faults.Count);
    }

    [DeterministicSimulation]
    private static async Task FaultScenarioAsync(SimulationContext context)
    {
        var delivered = false;
        var faults = new MessageFaultPlan()
            .Drop(1.0)
            .Delay(1.0, TimeSpan.FromSeconds(1));
        var bus = context.CreateMessageBus(
            new MessageBusOptions
            {
                Faults = faults,
                FaultScope = "tests.messaging"
            });

        bus.RegisterHandler<Ping>(
            "worker",
            (message, delivery, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                delivered = true;
                return Task.CompletedTask;
            });

        context.Invariants.Eventually(
            "message is eventually delivered",
            TimeSpan.FromSeconds(10),
            () => delivered);

        await bus.SendAsync("worker", new Ping(), context.CancellationToken);
    }

    [DeterministicSimulation]
    private static async Task RaceScenarioAsync(SimulationContext context)
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
                    throw new InvalidOperationException("reader observed stale state");
                }
            },
            context.CancellationToken);
    }

    private sealed record Ping;
}
