namespace Causalia.Storage.Faults;

/// <summary>
/// Adds virtual latency at a storage-operation boundary.
/// </summary>
public sealed record StorageDelayFault(TimeSpan Delay) : StorageFault;
