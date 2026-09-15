namespace Causalia.ProductionReality;

/// <summary>
/// Represents one correlated production flow whose relative start timing can be replayed deterministically.
/// </summary>
public sealed class ProductionRealityIncident
{
    internal ProductionRealityIncident(string correlationId, string sourceFingerprint, IReadOnlyList<ProductionRealityObservation> observations)
    {
        CorrelationId = correlationId;
        SourceFingerprint = sourceFingerprint;
        Observations = observations;
        StartedAt = observations[0].StartedAt;
        CompletedAt = observations.Max(value => value.StartedAt + value.Duration);
    }

    /// <summary>
    /// Gets the production correlation identifier.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets the stable fingerprint of the source production dataset.
    /// </summary>
    public string SourceFingerprint { get; }

    /// <summary>
    /// Gets the first observed production start instant.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets the latest observed production completion instant.
    /// </summary>
    public DateTimeOffset CompletedAt { get; }

    /// <summary>
    /// Gets the total wall-clock span represented by the correlated production flow.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Gets normalized production observations ordered by start instant and identifier.
    /// </summary>
    public IReadOnlyList<ProductionRealityObservation> Observations { get; }
}
