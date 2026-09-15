namespace Causalia.Storage.Exceptions;

/// <summary>
/// Represents a failed version-checked write against deterministic durable storage.
/// </summary>
public sealed class SimulationStorageConcurrencyException : Exception
{
    internal SimulationStorageConcurrencyException(string key, long expectedVersion, long actualVersion)
        : base($"Storage key '{key}' expected version {expectedVersion}, but observed version {actualVersion}.")
    {
        Key = key;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>
    /// Gets the storage key whose version check failed.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the version expected by the rejected write or delete.
    /// </summary>
    public long ExpectedVersion { get; }

    /// <summary>
    /// Gets the durable version that was actually observed.
    /// </summary>
    public long ActualVersion { get; }
}
