namespace Causalia.ProductionReality;

/// <summary>
/// Describes production evidence actually consumed by one deterministic simulation run.
/// </summary>
public sealed class ProductionRealityEvidence
{
    internal ProductionRealityEvidence(IReadOnlyList<ProductionRealityApplication> applications)
    {
        Applications = applications;
        SourceFingerprints = applications
            .Select(value => value.SourceFingerprint)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Gets applied production observations in deterministic execution order.
    /// </summary>
    public IReadOnlyList<ProductionRealityApplication> Applications { get; }

    /// <summary>
    /// Gets distinct source dataset fingerprints referenced by this run.
    /// </summary>
    public IReadOnlyList<string> SourceFingerprints { get; }

    /// <summary>
    /// Gets whether this simulation consumed production evidence.
    /// </summary>
    public bool HasEvidence => Applications.Count > 0;
}
