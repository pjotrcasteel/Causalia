namespace Causalia.Dapr;

/// <summary>
/// Represents a value read from a Dapr state store together with its ETag.
/// </summary>
public sealed class DaprStateValue<T>
{
    internal DaprStateValue(bool exists, T? value, string? etag)
    {
        Exists = exists;
        Value = value;
        ETag = etag;
    }

    /// <summary>
    /// Gets whether the state key exists.
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// Gets the deserialized state value.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the version ETag, or null when the key does not exist.
    /// </summary>
    public string? ETag { get; }
}
