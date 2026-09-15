using Causalia.Faults;

namespace Causalia.Tests.Faults;

[TestClass]
public sealed class FaultInjectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Evaluate_WithSameSeed_ProducesSameEffectsWithoutUsingSchedulerRandomness()
    {
        var first = await RunScenarioAsync(6001, false, TestContext.CancellationToken);
        var second = await RunScenarioAsync(6001, false, TestContext.CancellationToken);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task Evaluate_WhenUnrelatedPolicyIsAdded_KeepsExistingPolicyDecisionsStable()
    {
        var baseline = await RunScenarioAsync(6002, false, TestContext.CancellationToken);
        var withAdditionalPolicy = await RunScenarioAsync(6002, true, TestContext.CancellationToken);
        var stableEffects = withAdditionalPolicy.Where(effect => effect.StartsWith("stable:", StringComparison.Ordinal)).ToList();

        CollectionAssert.AreEqual(baseline, stableEffects);
    }

    [TestMethod]
    public async Task Evaluate_WithAlwaysPolicy_ReturnsEffect()
    {
        IReadOnlyList<string>? observed = null;
        var plan = new FaultPlan<string, string>()
            .Add(new ProbabilityFaultPolicy<string, string>("always", 1, value => $"fault:{value}"));

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 55 },
            context =>
            {
                observed = context.CreateFaultInjector("test.always", plan).Evaluate("operation");
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(observed);
        CollectionAssert.AreEqual(new List<string> { "fault:operation" }, observed.ToList());
    }

    [TestMethod]
    public void Add_WithDuplicatePolicyName_Throws()
    {
        var plan = new FaultPlan<int, string>()
            .Add(new ProbabilityFaultPolicy<int, string>("duplicate", 1, value => value.ToString()));

        Assert.ThrowsExactly<ArgumentException>(() =>
            plan.Add(new ProbabilityFaultPolicy<int, string>("duplicate", 1, value => value.ToString())));
    }

    [TestMethod]
    public async Task CreateFaultInjector_SnapshotsPlanAtCreationTime()
    {
        IReadOnlyList<string>? observed = null;
        var plan = new FaultPlan<int, string>()
            .Add(new ProbabilityFaultPolicy<int, string>("first", 1, _ => "first"));

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 6003 },
            context =>
            {
                var injector = context.CreateFaultInjector("snapshot", plan);
                plan.Add(new ProbabilityFaultPolicy<int, string>("second", 1, _ => "second"));
                observed = injector.Evaluate(1);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(observed);
        CollectionAssert.AreEqual(new List<string> { "first" }, observed.ToList());
    }

    private static async Task<List<string>> RunScenarioAsync(
        ulong seed,
        bool includeAdditionalPolicy,
        CancellationToken cancellationToken)
    {
        var observed = new List<string>();
        var plan = new FaultPlan<int, string>();

        if (includeAdditionalPolicy)
        {
            plan.Add(new ProbabilityFaultPolicy<int, string>("additional", 0.5, value => $"additional:{value}"));
        }

        plan.Add(new ProbabilityFaultPolicy<int, string>("stable", 0.5, value => $"stable:{value}"));

        await Simulation.RunAsync(
            new SimulationOptions { Seed = seed },
            context =>
            {
                var injector = context.CreateFaultInjector("test.replay", plan);

                for (var index = 0; index < 20; index++)
                {
                    observed.AddRange(injector.Evaluate(index));
                }

                return Task.CompletedTask;
            },
            cancellationToken);

        return observed;
    }
}
