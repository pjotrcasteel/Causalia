using Causalia.Faults;

namespace Causalia.Tests.Faults;

[TestClass]
public sealed class TypedFaultRuleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TypedRule_MatchesAndLimitsOccurrences_PerInjector()
    {
        var plan = new FaultPlan<BoundaryEvent, string>();
        plan.On<Committed>("lost-commit-ack").Where(value => value.OrderId == 42).Once()
            .Apply(_ => "ack-lost");

        await Simulation.RunAsync(
            context =>
            {
                var first = context.CreateFaultInjector("typed.first", plan);
                CollectionAssert.AreEqual(new[] { "ack-lost" }, first.Evaluate(new Committed(42)).ToArray());
                Assert.AreEqual(0, first.Evaluate(new Committed(42)).Count);

                var second = context.CreateFaultInjector("typed.second", plan);
                Assert.AreEqual(0, second.Evaluate(new Delivered(42)).Count);
                CollectionAssert.AreEqual(new[] { "ack-lost" }, second.Evaluate(new Committed(42)).ToArray());
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public void TypedRule_RejectsInvalidCountAndDuplicateNames()
    {
        var plan = new FaultPlan<BoundaryEvent, string>();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => plan.On<Committed>("fault").Times(0));
        plan.On<Committed>("fault").Apply(_ => "first");
        Assert.ThrowsExactly<ArgumentException>(() => plan.On<Delivered>("fault").Apply(_ => "second"));
    }

    private abstract record BoundaryEvent;
    private sealed record Committed(int OrderId) : BoundaryEvent;
    private sealed record Delivered(int OrderId) : BoundaryEvent;
}
