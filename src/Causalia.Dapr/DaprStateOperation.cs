namespace Causalia.Dapr;

/// <summary>
/// Describes one Dapr state transaction operation.
/// </summary>
public sealed class DaprStateOperation
{
    internal DaprStateOperation(string key, ReadOnlyMemory<byte>? value, string? etag)
    {
        Key = key;
        Value = value;
        ETag = etag;
    }

    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets serialized state, or null for a delete operation.
    /// </summary>
    public ReadOnlyMemory<byte>? Value { get; }

    /// <summary>
    /// Gets an optional optimistic-concurrency ETag.
    /// </summary>
    public string? ETag { get; }

    /// <summary>
    /// Creates an upsert operation.
    /// </summary>
    public static DaprStateOperation Upsert<T>(string key, T value, string? etag = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new DaprStateOperation(key, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value), etag);
    }

    /// <summary>
    /// Creates a delete operation.
    /// </summary>
    public static DaprStateOperation Delete(string key, string? etag = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new DaprStateOperation(key, null, etag);
    }
}
