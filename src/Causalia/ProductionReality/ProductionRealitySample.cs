namespace Causalia.ProductionReality;

/// <summary>
/// Describes one deterministic sample selected from production evidence.
/// </summary>
public sealed class ProductionRealitySample
{
    internal ProductionRealitySample(string sourceFingerprint, ProductionRealityObservation observation)
    {
        SourceFingerprint = sourceFingerprint;
        Observation = observation;
    }

    /// <summary>
    /// Gets the stable fingerprint of the source production dataset.
    /// </summary>
    public string SourceFingerprint { get; }

    /// <summary>
    /// Gets the exact normalized production observation selected by the bridge.
    /// </summary>
    public ProductionRealityObservation Observation { get; }

    /// <summary>
    /// Gets the logical operation name.
    /// </summary>
    public string Operation => Observation.Operation;

    /// <summary>
    /// Gets the production duration applied to virtual time.
    /// </summary>
    public TimeSpan Duration => Observation.Duration;

    /// <summary>
    /// Gets the production outcome associated with this sample.
    /// </summary>
    public ProductionRealityOutcome Outcome => Observation.Outcome;
}
