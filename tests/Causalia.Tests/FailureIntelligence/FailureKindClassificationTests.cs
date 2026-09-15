using Causalia.Consistency;
using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.Linearizability;
using Causalia.ModelBased;

namespace Causalia.Tests.FailureIntelligence;

[TestClass]
public sealed class FailureKindClassificationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FailureKind_ClassifiesConsistencyViolation()
    {
        var failure = await CaptureFailureAsync(8101, ConsistencyScenarioAsync, TestContext.CancellationToken);

        Assert.AreEqual(FailureKind.ConsistencyViolation, failure.Kind);
    }

    [TestMethod]
    public async Task FailureKind_ClassifiesLinearizabilityViolation()
    {
        var failure = await CaptureFailureAsync(8102, LinearizabilityScenarioAsync, TestContext.CancellationToken);

        Assert.AreEqual(FailureKind.LinearizabilityViolation, failure.Kind);
    }

    [TestMethod]
    public async Task FailureKind_ClassifiesDeadlock()
    {
        var failure = await CaptureFailureAsync(8103, DeadlockScenarioAsync, TestContext.CancellationToken);

        Assert.AreEqual(FailureKind.Deadlock, failure.Kind);
    }

    [TestMethod]
    public async Task FailureKind_ClassifiesStepLimit()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 8104, MaxSteps = 2 },
                StepLimitScenarioAsync,
                TestContext.CancellationToken));

        Assert.AreEqual(FailureKind.StepLimitExceeded, failure.Kind);
    }

    [TestMethod]
    public async Task FailureKind_ClassifiesModelViolation()
    {
        var specification = CreateFailingModel();
        var failure = await Assert.ThrowsExactlyAsync<SimulationModelExplorationFailedException>(async () =>
            await Simulation.CheckModelAsync(
                new ModelBasedOptions
                {
                    Simulation = new SimulationOptions { Seed = 8105 },
                    MaxSequences = 1,
                    MaxCommandDepth = 1
                },
                specification,
                TestContext.CancellationToken));

        Assert.AreEqual(FailureKind.ModelViolation, failure.Failure.Kind);
    }

    private static async Task<SimulationFailedException> CaptureFailureAsync(
        ulong seed,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        return await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(new SimulationOptions { Seed = seed }, scenario, cancellationToken));
    }

    [DeterministicSimulation]
    private static Task ConsistencyScenarioAsync(SimulationContext context)
    {
        var history = context.Consistency.CreateHistory<string>("classification")
            .Require(ConsistencyGuarantees.ReadYourWrites);
        history.Write("session", "primary", "key");
        history.Read("session", "secondary", "key", null);
        return Task.CompletedTask;
    }

    [DeterministicSimulation]
    private static Task LinearizabilityScenarioAsync(SimulationContext context)
    {
        var history = context.Linearizability.CreateHistory<string, int>("classification");
        history.Begin("client", "read").Complete(1);
        var specification = new LinearizabilitySpecification<int, string, int>(
            0,
            static (state, _, output) => output == state
                ? LinearizabilityStep<int>.Accept(state)
                : LinearizabilityStep<int>.Reject(state));
        history.RequireLinearizable(specification);
        return Task.CompletedTask;
    }

    [DeterministicSimulation]
    private static async Task DeadlockScenarioAsync(SimulationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await completion.Task;
    }

    [DeterministicSimulation]
    private static async Task StepLimitScenarioAsync(SimulationContext context)
    {
        for (var index = 0; index < 10; index++)
        {
            await Task.Yield();
            context.CancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static ModelBasedSpecification<int, object> CreateFailingModel()
    {
        return new ModelBasedSpecification<int, object>(
            "classification-model",
            static () => 0,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new object());
            },
            static _ => new List<ModelCommand<int, object>>
            {
                ModelCommand.Create<int, object, int>(
                    "advance",
                    value => value + 1,
                    static (_, _, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(0);
                    },
                    static (_, expected, observed) => ModelCommandVerification.Equal(expected, observed))
            });
    }
}
