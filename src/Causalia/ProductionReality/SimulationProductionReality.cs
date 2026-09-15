using Causalia.Randomness;
using Causalia.Runtime;

namespace Causalia.ProductionReality;

/// <summary>
/// Applies normalized production evidence to deterministic virtual time without perturbing scheduler or user-random streams.
/// </summary>
public sealed class SimulationProductionReality
{
    private readonly List<ProductionRealityApplication> _applications = new();
    private readonly DeterministicRandom _random;
    private readonly DeterministicScheduler _scheduler;
    private ProductionRealityProfile? _profile;

    internal SimulationProductionReality(DeterministicScheduler scheduler, ulong seed)
    {
        _scheduler = scheduler;
        _random = new DeterministicRandom(seed ^ 0xD1B54A32D192ED03UL);
    }

    /// <summary>
    /// Attaches production evidence used by subsequent sampled operation calls.
    /// </summary>
    public void Use(ProductionRealityDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        _profile = dataset.CreateProfile();
        _scheduler.RecordTrace($"reality:dataset:{dataset.Fingerprint}:attached:{dataset.Observations.Count}");
    }

    /// <summary>
    /// Selects one exact observed production sample for an operation without advancing virtual time.
    /// </summary>
    public ProductionRealitySample Sample(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        var profile = _profile ?? throw new InvalidOperationException("No production reality dataset is attached. Call Use(...) first.");
        var operationProfile = profile.GetOperation(operation);
        var index = _random.NextInt32(operationProfile.Observations.Count);
        var observation = operationProfile.Observations[index];
        Record(ProductionRealityApplicationMode.Sampled, profile.SourceFingerprint, observation);
        return new ProductionRealitySample(profile.SourceFingerprint, observation);
    }

    /// <summary>
    /// Selects one exact production sample and advances virtual time by its observed duration.
    /// </summary>
    public async Task<ProductionRealitySample> ApplyAsync(string operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sample = Sample(operation);
        await Task.Delay(sample.Duration, _scheduler.TimeProvider, cancellationToken);
        return sample;
    }

    /// <summary>
    /// Replays the relative production start timing of one correlated incident and invokes the supplied deterministic handler for every observation.
    /// </summary>
    public async Task ReplayIncidentAsync(
        ProductionRealityIncident incident,
        Func<ProductionRealityObservation, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();
        var tasks = incident.Observations
            .Select(value => ReplayObservationAsync(incident, value, handler, cancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Advances virtual time by the duration of one exact production observation.
    /// </summary>
    public Task DelayObservedDurationAsync(ProductionRealityObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return Task.Delay(observation.Duration, _scheduler.TimeProvider, cancellationToken);
    }

    internal ProductionRealityEvidence CreateEvidence()
    {
        return new ProductionRealityEvidence(new List<ProductionRealityApplication>(_applications).AsReadOnly());
    }

    private async Task ReplayObservationAsync(
        ProductionRealityIncident incident,
        ProductionRealityObservation observation,
        Func<ProductionRealityObservation, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var offset = observation.StartedAt - incident.StartedAt;

        if (offset > TimeSpan.Zero)
        {
            await Task.Delay(offset, _scheduler.TimeProvider, cancellationToken);
        }

        Record(ProductionRealityApplicationMode.IncidentReplay, incident.SourceFingerprint, observation);
        await handler(observation, cancellationToken);
    }

    private void Record(ProductionRealityApplicationMode mode, string fingerprint, ProductionRealityObservation observation)
    {
        _applications.Add(new ProductionRealityApplication(mode, fingerprint, observation, _scheduler.TimeProvider.GetUtcNow()));
        _scheduler.RecordTrace($"reality:{mode}:{observation.Operation}:{observation.Id}:{observation.Outcome}:{observation.Duration.Ticks}");
    }
}
