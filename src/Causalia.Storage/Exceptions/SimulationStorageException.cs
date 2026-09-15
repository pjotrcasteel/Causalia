namespace Causalia.Storage.Exceptions;

/// <summary>
/// Base exception for deterministic storage failures.
/// </summary>
public abstract class SimulationStorageException : Exception
{
    /// <summary>
    /// Initializes a deterministic storage failure.
    /// </summary>
    protected SimulationStorageException(string message, string storageName, long operationId, string code)
        : base(message)
    {
        StorageName = storageName;
        OperationId = operationId;
        Code = code;
    }

    /// <summary>
    /// Gets the logical storage boundary where the failure occurred.
    /// </summary>
    public string StorageName { get; }

    /// <summary>
    /// Gets the deterministic storage operation identifier.
    /// </summary>
    public long OperationId { get; }

    /// <summary>
    /// Gets the stable failure code supplied by the configured storage fault.
    /// </summary>
    public string Code { get; }
}
