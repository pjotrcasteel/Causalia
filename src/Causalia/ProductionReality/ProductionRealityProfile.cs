namespace Causalia.ProductionReality;

/// <summary>
/// Provides operation-level timing and outcome distributions derived from production evidence.
/// </summary>
public sealed class ProductionRealityProfile
{
    private readonly IReadOnlyDictionary<string, ProductionRealityOperationProfile> _operations;

    internal ProductionRealityProfile(string sourceFingerprint, IReadOnlyDictionary<string, ProductionRealityOperationProfile> operations)
    {
        SourceFingerprint = sourceFingerprint;
        _operations = operations;
    }

    /// <summary>
    /// Gets the stable fingerprint of the source production dataset.
    /// </summary>
    public string SourceFingerprint { get; }

    /// <summary>
    /// Gets operation profiles ordered by logical operation name.
    /// </summary>
    public IReadOnlyList<ProductionRealityOperationProfile> Operations => _operations.Values.OrderBy(value => value.Operation).ToArray();

    /// <summary>
    /// Gets a production profile for one logical operation.
    /// </summary>
    public ProductionRealityOperationProfile GetOperation(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return _operations.TryGetValue(operation, out var profile)
            ? profile
            : throw new KeyNotFoundException($"Production reality does not contain operation '{operation}'.");
    }
}
