using Causalia.ModelBased;
using Causalia.Scheduling;
using Causalia.Verification;

namespace Causalia.Tests.Verification;

[TestClass]
public sealed class VerificationPlatformTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task VerifyAsync_WithMixedPassingPlan_ReturnsPortableStableReport()
    {
        var plan = VerificationPlan.Create(
            "release-gate",
            builder => builder
                .AddRun(
                    "smoke",
                    new SimulationOptions { Seed = 2001 },
                    static context => Task.Delay(TimeSpan.FromMilliseconds(5), context.TimeProvider, context.CancellationToken))
                .AddExploration(
                    "schedules",
                    new ExplorationOptions
                    {
                        Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
                        Simulation = new SimulationOptions { Seed = 2002 },
                        MaxSchedules = 10,
                        MaxDecisionDepth = 10
                    },
                    static _ => Task.CompletedTask)
                .AddModel(
                    "model",
                    new ModelBasedOptions
                    {
                        Simulation = new SimulationOptions { Seed = 2003 },
                        MaxSequences = 5,
                        MaxCommandDepth = 2
                    },
                    CreateModel()));

        var result = await Simulation.VerifyAsync(plan, new VerificationRunOptions(), TestContext.CancellationToken);
        var roundTrip = VerificationReport.ParseJson(result.Report.ToJson());

        Assert.IsTrue(result.Passed);
        Assert.AreEqual(3, result.PassedCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(result.Report.Fingerprint, roundTrip.Fingerprint);
        Assert.IsTrue(result.Report.Fingerprint.StartsWith("vp1:", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task VerificationReport_WhenPortableContentIsModified_RejectsFingerprint()
    {
        var plan = VerificationPlan.Create(
            "tamper-detection",
            builder => builder.AddRun(
                "pass",
                new SimulationOptions { Seed = 2004 },
                static _ => Task.CompletedTask));
        var result = await Simulation.VerifyAsync(plan, new VerificationRunOptions(), TestContext.CancellationToken);
        var json = result.Report.ToJson();
        var modified = json.Replace("Completed 0 scheduler steps", "Completed 999 scheduler steps", StringComparison.Ordinal);

        Assert.ThrowsExactly<FormatException>(() => VerificationReport.ParseJson(modified));
    }

    [TestMethod]
    public async Task VerifyAsync_WithEquivalentFailures_ClustersThemAcrossSteps()
    {
        var plan = VerificationPlan.Create(
            "failure-clustering",
            builder => builder
                .AddRun("first", new SimulationOptions { Seed = 2010 }, FailAsync)
                .AddRun("second", new SimulationOptions { Seed = 2011 }, FailAsync));

        var result = await Simulation.VerifyAsync(plan, new VerificationRunOptions(), TestContext.CancellationToken);

        Assert.IsFalse(result.Passed);
        Assert.AreEqual(2, result.FailedCount);
        Assert.AreEqual(1, result.FailureIntelligence.UniqueFailureCount);
        Assert.AreEqual(2, result.FailureIntelligence.Clusters[0].OccurrenceCount);
        Assert.AreEqual(1, result.Report.Failures.Count);
    }

    [TestMethod]
    public async Task VerifyAsync_WithStopOnFirstFailure_SkipsRemainingSteps()
    {
        var plan = VerificationPlan.Create(
            "fail-fast",
            builder => builder
                .AddRun("failure", new SimulationOptions { Seed = 2020 }, FailAsync)
                .AddRun("never", new SimulationOptions { Seed = 2021 }, static _ => Task.CompletedTask));

        var result = await Simulation.VerifyAsync(
            plan,
            new VerificationRunOptions { StopOnFirstFailure = true },
            TestContext.CancellationToken);

        Assert.AreEqual(VerificationStepStatus.Failed, result.Steps[0].Status);
        Assert.AreEqual(VerificationStepStatus.Skipped, result.Steps[1].Status);
        Assert.AreEqual(1, result.SkippedCount);
    }

    [TestMethod]
    public async Task EnsurePassed_WhenPlanFailed_ThrowsStructuredPlatformException()
    {
        var plan = VerificationPlan.Create(
            "gate",
            builder => builder.AddRun("failure", new SimulationOptions { Seed = 2030 }, FailAsync));
        var result = await Simulation.VerifyAsync(plan, new VerificationRunOptions(), TestContext.CancellationToken);

        var failure = Assert.ThrowsExactly<VerificationRunFailedException>(result.EnsurePassed);

        Assert.AreSame(result, failure.Result);
        Assert.IsTrue(failure.Message.Contains(result.Report.Fingerprint, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Plan_WithDuplicateStepName_IsRejected()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            VerificationPlan.Create(
                "duplicates",
                builder => builder
                    .AddRun("same", new SimulationOptions { Seed = 2040 }, static _ => Task.CompletedTask)
                    .AddRun("same", new SimulationOptions { Seed = 2041 }, static _ => Task.CompletedTask)));
    }

    [DeterministicSimulation]
    private static Task FailAsync(SimulationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("same defect");
    }

    private static ModelBasedSpecification<CounterState, CounterSystem> CreateModel()
    {
        return new ModelBasedSpecification<CounterState, CounterSystem>(
            "counter",
            static () => new CounterState(0),
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CounterSystem());
            },
            state => state.Value == 0
                ? [ModelCommand.Create<CounterState, CounterSystem, int>(
                    "increment",
                    current => current with { Value = 1 },
                    static (system, _, cancellationToken) => system.IncrementAsync(cancellationToken),
                    static (_, expected, observed) => ModelCommandVerification.Equal(expected.Value, observed))]
                : []);
    }

    private sealed record CounterState(int Value);

    private sealed class CounterSystem
    {
        private int _value;

        public Task<int> IncrementAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _value++;
            return Task.FromResult(_value);
        }
    }
}
