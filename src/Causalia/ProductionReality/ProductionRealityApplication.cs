namespace Causalia.ProductionReality;

/// <summary>
/// Records one piece of production evidence applied during deterministic simulation.
/// </summary>
public sealed class ProductionRealityApplication
{
    internal ProductionRealityApplication(
        ProductionRealityApplicationMode mode,
        string sourceFingerprint,
        ProductionRealityObservation observation,
        DateTimeOffset appliedAt)
    {
        Mode = mode;
        SourceFingerprint = sourceFingerprint;
        ObservationId = observation.Id;
        Operation = observation.Operation;
        Outcome = observation.Outcome;
        ObservedDuration = observation.Duration;
        CorrelationId = observation.CorrelationId;
        AppliedAt = appliedAt;
    }

    /// <summary>
    /// Gets how the production evidence was applied.
    /// </summary>
    public ProductionRealityApplicationMode Mode { get; }

    /// <summary>
    /// Gets the stable fingerprint of the source production dataset.
    /// </summary>
    public string SourceFingerprint { get; }

    /// <summary>
    /// Gets the selected production observation identifier.
    /// </summary>
    public string ObservationId { get; }

    /// <summary>
    /// Gets the logical operation name.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the observed production outcome.
    /// </summary>
    public ProductionRealityOutcome Outcome { get; }

    /// <summary>
    /// Gets the observed production duration.
    /// </summary>
    public TimeSpan ObservedDuration { get; }

    /// <summary>
    /// Gets the production correlation identifier when one was available.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gets the virtual instant at which this evidence was applied.
    /// </summary>
    public DateTimeOffset AppliedAt { get; }
}
