namespace Causalia.Storage;

/// <summary>
/// Describes one deterministic storage-boundary evaluation.
/// </summary>
public sealed class StorageOperationContext
{
    internal StorageOperationContext(long operationId, string storageName, StorageOperationKind kind, StorageOperationPhase phase, string? key)
    {
        OperationId = operationId;
        StorageName = storageName;
        Kind = kind;
        Phase = phase;
        Key = key;
    }

    /// <summary>
    /// Gets the deterministic operation identifier.
    /// </summary>
    public long OperationId { get; }

    /// <summary>
    /// Gets the stable logical storage name.
    /// </summary>
    public string StorageName { get; }

    /// <summary>
    /// Gets the logical operation kind.
    /// </summary>
    public StorageOperationKind Kind { get; }

    /// <summary>
    /// Gets the boundary phase currently being evaluated.
    /// </summary>
    public StorageOperationPhase Phase { get; }

    /// <summary>
    /// Gets the affected key when the operation targets one key.
    /// </summary>
    public string? Key { get; }
}
