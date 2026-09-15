namespace Causalia.Storage;

/// <summary>
/// Represents one versioned value read from deterministic durable storage.
/// </summary>
public sealed class StorageReadResult
{
    internal StorageReadResult(bool exists, ReadOnlyMemory<byte> value, long version)
    {
        Exists = exists;
        Value = value;
        Version = version;
    }

    /// <summary>
    /// Gets whether the key existed.
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// Gets an immutable copy of the stored value.
    /// </summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <summary>
    /// Gets the current per-key version, or zero when the key did not exist.
    /// </summary>
    public long Version { get; }
}
