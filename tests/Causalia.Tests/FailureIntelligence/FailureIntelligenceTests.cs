using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.Faults;
using Causalia.Messaging;
using Causalia.Messaging.Faults;
using Causalia.Scheduling;

namespace Causalia.Tests.FailureIntelligence;

[TestClass]
public sealed class FailureIntelligenceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Signature_WhenSameSemanticFailureUsesDifferentSeeds_RemainsStable()
    {
        var first = await CaptureFailureAsync(111, InvariantScenarioAsync, TestContext.CancellationToken);
        var second = await CaptureFailureAsync(222, InvariantScenarioAsync, TestContext.CancellationToken);

        Assert.AreEqual(FailureKind.InvariantViolation, first.Kind);
        Assert.AreEqual(first.Signature.Token, second.Signature.Token);
        Assert.IsTrue(first.Signature.Token.StartsWith("fi2:", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Signature_WhenUnhandledFailureMessageChanges_ProducesDifferentSignature()
    {
        var first = await CaptureFailureAsync(333, context => ThrowAsync(context, "failure-a"), TestContext.CancellationToken);
        var second = await CaptureFailureAsync(333, context => ThrowAsync(context, "failure-b"), TestContext.CancellationToken);

        Assert.AreEqual(FailureKind.UnhandledException, first.Kind);
        Assert.AreNotEqual(first.Signature.Token, second.Signature.Token);
    }

    [TestMethod]
    public async Task AnalyzeFailureAsync_WhenFaultIsEssential_AttributesFaultInjection()
    {
        var options = new SimulationOptions { Seed = 444 };
        var failure = await CaptureFailureAsync(options.Seed, FaultScenarioAsync, TestContext.CancellationToken);

        var analysis = await Simulation.AnalyzeFailureAsync(
            options,
            new FailureAnalysisOptions(),
            failure,
            FaultScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(FailureTrigger.FaultInjection, analysis.Trigger);
        Assert.AreEqual(FailureAnalysisConfidence.Minimized, analysis.Confidence);
        Assert.AreEqual(1, analysis.EssentialFaults.Count);
        Assert.AreEqual(0, analysis.EssentialSchedulerChoices.Count);
        Assert.IsNotNull(analysis.ReproductionToken);
    }

    [TestMethod]
    public async Task AnalyzeFailureAsync_WhenSchedulerOrderingIsEssential_AttributesSchedulerOrdering()
    {
        var options = new SimulationOptions { Seed = 555 };
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

        var analysis = await Simulation.AnalyzeFailureAsync(
            options,
            new FailureAnalysisOptions(),
            explorationFailure.Failure,
            RaceScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(FailureTrigger.SchedulerOrdering, analysis.Trigger);
        Assert.IsTrue(analysis.EssentialSchedulerChoices.Count > 0);
        Assert.AreEqual(0, analysis.EssentialFaults.Count);
    }

    [TestMethod]
    public async Task AnalyzeFailureAsync_WhenFaultAndOrderingAreEssential_AttributesMixedTrigger()
    {
        var options = new SimulationOptions { Seed = 556 };
        var explorationFailure = await Assert.ThrowsExactlyAsync<SimulationExplorationFailedException>(async () =>
            await Simulation.ExploreAsync(
                new ExplorationOptions
                {
                    Simulation = options,
                    MaxSchedules = 20,
                    MaxDecisionDepth = 20
                },
                MixedScenarioAsync,
                TestContext.CancellationToken));

        var analysis = await Simulation.AnalyzeFailureAsync(
            options,
            new FailureAnalysisOptions(),
            explorationFailure.Failure,
            MixedScenarioAsync,
            TestContext.CancellationToken);

        Assert.AreEqual(FailureTrigger.SchedulerOrderingAndFaultInjection, analysis.Trigger);
        Assert.IsTrue(analysis.EssentialSchedulerChoices.Count > 0);
        Assert.AreEqual(1, analysis.EssentialFaults.Count);
    }

    [TestMethod]
    public async Task CreateReport_WhenFailuresRepeat_ClustersAndPrioritizesByOccurrenceCount()
    {
        var repeatedOne = FailureAnalyzer.Describe(
            await CaptureFailureAsync(601, InvariantScenarioAsync, TestContext.CancellationToken));
        var repeatedTwo = FailureAnalyzer.Describe(
            await CaptureFailureAsync(602, InvariantScenarioAsync, TestContext.CancellationToken));
        var different = FailureAnalyzer.Describe(
            await CaptureFailureAsync(603, context => ThrowAsync(context, "different"), TestContext.CancellationToken));
        IReadOnlyList<FailureAnalysis> analyses = new List<FailureAnalysis> { repeatedOne, repeatedTwo, different };

        var report = FailureAnalyzer.CreateReport(analyses);

        Assert.AreEqual(3, report.TotalOccurrences);
        Assert.AreEqual(2, report.UniqueFailureCount);
        Assert.AreEqual(1, report.Clusters[0].TriageRank);
        Assert.AreEqual(2, report.Clusters[0].OccurrenceCount);
        CollectionAssert.AreEqual(new List<ulong> { 601, 602 }, report.Clusters[0].Seeds.ToList());
    }

    [TestMethod]
    public async Task Describe_WhenNoMinimizationRan_LeavesTriggerUnknownAndKeepsTraceContextBounded()
    {
        var failure = await CaptureFailureAsync(700, InvariantScenarioAsync, TestContext.CancellationToken);

        var analysis = FailureAnalyzer.Describe(failure, traceContextEntries: 1);

        Assert.AreEqual(FailureTrigger.Unknown, analysis.Trigger);
        Assert.AreEqual(FailureAnalysisConfidence.Descriptive, analysis.Confidence);
        Assert.IsTrue(analysis.TraceContext.Count <= 1);
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
    private static async Task InvariantScenarioAsync(SimulationContext context)
    {
        context.Invariants.Never("forbidden-state", () => true);
        await Task.Yield();
        context.CancellationToken.ThrowIfCancellationRequested();
    }

    private static Task ThrowAsync(SimulationContext context, string message)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(message);
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
                FaultScope = "failure-intelligence"
            });

        bus.RegisterHandler<Ping>(
            "worker",
            (message, delivery, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                delivered = true;
                return Task.CompletedTask;
            });
        context.Invariants.Eventually("message-delivered", TimeSpan.FromSeconds(10), () => delivered);
        await bus.SendAsync("worker", new Ping(), context.CancellationToken);
    }

    [DeterministicSimulation]
    private static async Task MixedScenarioAsync(SimulationContext context)
    {
        var plan = new FaultPlan<string, string>()
            .Add(new ProbabilityFaultPolicy<string, string>("failure-intelligence.mixed", 1.0, _ => "fault"));
        var faulted = context.CreateFaultInjector("failure-intelligence.mixed", plan).Evaluate("operation").Count > 0;
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

                if (faulted && value == 0)
                {
                    throw new InvalidOperationException("fault-and-ordering");
                }
            },
            context.CancellationToken);
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
                    throw new InvalidOperationException("reader-observed-stale-state");
                }
            },
            context.CancellationToken);
    }

    private sealed record Ping;
}
