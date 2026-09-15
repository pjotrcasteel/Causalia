using Causalia.Exceptions;
using Causalia.ProductionReality;

namespace Causalia.Tests.ProductionReality;

[TestClass]
public sealed class ProductionRealityTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Dataset_JsonRoundTrip_PreservesStableFingerprint()
    {
        var dataset = CreateDataset();
        var roundTrip = ProductionRealityDataset.ParseJson(dataset.ToJson());

        Assert.AreEqual(dataset.Fingerprint, roundTrip.Fingerprint);
        Assert.AreEqual(dataset.Source, roundTrip.Source);
        Assert.AreEqual(dataset.Observations.Count, roundTrip.Observations.Count);
    }

    [TestMethod]
    public void Dataset_CreateProfile_ReportsObservedDistribution()
    {
        var profile = CreateDataset().CreateProfile().GetOperation("payments.authorize");

        Assert.AreEqual(3, profile.SampleCount);
        Assert.AreEqual(1d / 3d, profile.FailureRate, 0.000001d);
        Assert.AreEqual(TimeSpan.FromMilliseconds(80), profile.Percentile95);
        Assert.AreEqual(TimeSpan.FromMilliseconds(80), profile.MaximumDuration);
    }

    [TestMethod]
    public void Dataset_Fingerprint_DistinguishesDelimiterPlacement()
    {
        var startedAt = DateTimeOffset.Parse("2026-09-13T12:00:00Z");
        var first = ProductionRealityDataset.Create(
            "production",
            [Observation("a|b", "c", startedAt, 1, ProductionRealityOutcome.Success, "correlation")]);
        var second = ProductionRealityDataset.Create(
            "production",
            [Observation("a", "b|c", startedAt, 1, ProductionRealityOutcome.Success, "correlation")]);

        Assert.AreNotEqual(first.ToJson(), second.ToJson());
        Assert.AreNotEqual(first.Fingerprint, second.Fingerprint);
        Assert.IsTrue(first.Fingerprint.StartsWith("pr2:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Dataset_ParseJson_AcceptsLegacyPr1Schema()
    {
        var json = CreateDataset().ToJson().Replace("\"pr2\"", "\"pr1\"", StringComparison.Ordinal);

        var restored = ProductionRealityDataset.ParseJson(json);

        Assert.IsTrue(restored.Fingerprint.StartsWith("pr2:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Dataset_GetIncident_PreservesProductionStartOrder()
    {
        var incident = CreateDataset().GetIncident("checkout-42");

        Assert.AreEqual(2, incident.Observations.Count);
        Assert.AreEqual("inventory.reserve", incident.Observations[0].Operation);
        Assert.AreEqual("payments.authorize", incident.Observations[1].Operation);
        Assert.AreEqual(TimeSpan.FromMilliseconds(55), incident.Duration);
    }

    [TestMethod]
    public async Task ApplyAsync_WithSameSeed_SelectsSameProductionObservation()
    {
        var first = await RunSampleAsync(1901);
        var second = await RunSampleAsync(1901);

        Assert.AreEqual(first.ObservationId, second.ObservationId);
        Assert.AreEqual(first.ObservedDuration, second.ObservedDuration);
    }

    [TestMethod]
    public async Task ApplyAsync_DoesNotPerturbUserRandomStream()
    {
        var dataset = CreateDataset();
        int withReality = 0;
        int withoutReality = 0;

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1902 },
            async context =>
            {
                context.ProductionReality.Use(dataset);
                await context.ProductionReality.ApplyAsync("payments.authorize", context.CancellationToken);
                withReality = context.Random.NextInt32(1_000_000);
            },
            TestContext.CancellationToken);
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1902 },
            context =>
            {
                withoutReality = context.Random.NextInt32(1_000_000);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(withoutReality, withReality);
    }

    [TestMethod]
    public async Task ReplayIncidentAsync_ReplaysRelativeStartTiming()
    {
        var dataset = CreateDataset();
        var incident = dataset.GetIncident("checkout-42");
        var starts = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1903 },
            async context =>
            {
                var origin = context.TimeProvider.GetTimestamp();
                await context.ProductionReality.ReplayIncidentAsync(
                    incident,
                    async (observation, cancellationToken) =>
                    {
                        starts[observation.Operation] = context.TimeProvider.GetElapsedTime(origin);
                        await context.ProductionReality.DelayObservedDurationAsync(observation, cancellationToken);
                    },
                    context.CancellationToken);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.Zero, starts["inventory.reserve"]);
        Assert.AreEqual(TimeSpan.FromMilliseconds(5), starts["payments.authorize"]);
        Assert.AreEqual(2, result.ProductionReality.Applications.Count);
    }

    [TestMethod]
    public async Task FailedRun_RetainsProductionEvidence()
    {
        var dataset = CreateDataset();
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                new SimulationOptions { Seed = 1904 },
                async context =>
                {
                    context.ProductionReality.Use(dataset);
                    await context.ProductionReality.ApplyAsync("payments.authorize", context.CancellationToken);
                    throw new InvalidOperationException("boom");
                },
                TestContext.CancellationToken));

        Assert.AreEqual(1, failure.ProductionReality.Applications.Count);
        Assert.AreEqual(dataset.Fingerprint, failure.ProductionReality.SourceFingerprints[0]);
    }

    private async Task<ProductionRealityApplication> RunSampleAsync(ulong seed)
    {
        var dataset = CreateDataset();
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = seed },
            async context =>
            {
                context.ProductionReality.Use(dataset);
                await context.ProductionReality.ApplyAsync("payments.authorize", context.CancellationToken);
            },
            TestContext.CancellationToken);
        return result.ProductionReality.Applications.Single();
    }

    private static ProductionRealityDataset CreateDataset()
    {
        var start = DateTimeOffset.Parse("2026-09-13T12:00:00Z");
        return ProductionRealityDataset.Create(
            "production",
            new[]
            {
                Observation("inventory-1", "inventory.reserve", start, 20, ProductionRealityOutcome.Success, "checkout-42"),
                Observation("payment-1", "payments.authorize", start.AddMilliseconds(5), 50, ProductionRealityOutcome.Failure, "checkout-42"),
                Observation("payment-2", "payments.authorize", start.AddSeconds(1), 10, ProductionRealityOutcome.Success, "checkout-43"),
                Observation("payment-3", "payments.authorize", start.AddSeconds(2), 80, ProductionRealityOutcome.Success, "checkout-44")
            });
    }

    private static ProductionRealityObservation Observation(
        string id,
        string operation,
        DateTimeOffset startedAt,
        int durationMilliseconds,
        ProductionRealityOutcome outcome,
        string correlationId)
    {
        return new ProductionRealityObservation
        {
            Id = id,
            Operation = operation,
            StartedAt = startedAt,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            Outcome = outcome,
            CorrelationId = correlationId
        };
    }
}
