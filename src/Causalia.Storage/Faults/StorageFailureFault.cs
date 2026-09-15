namespace Causalia.Storage.Faults;

/// <summary>
/// Fails a storage-operation boundary with a stable diagnostic code.
/// </summary>
public sealed record StorageFailureFault(string Code) : StorageFault;
