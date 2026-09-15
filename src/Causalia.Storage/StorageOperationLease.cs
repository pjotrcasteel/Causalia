namespace Causalia.Storage;

/// <summary>
/// Correlates the before and after boundaries of one durable-storage operation.
/// </summary>
public sealed class StorageOperationLease
{
    private readonly SimulationStorageBoundary _owner;
    private int _terminal;

    internal StorageOperationLease(SimulationStorageBoundary owner, long operationId, StorageOperationKind kind, string? key)
    {
        _owner = owner;
        OperationId = operationId;
        Kind = kind;
        Key = key;
        StorageName = owner.StorageName;
    }

    /// <summary>
    /// Gets the deterministic operation identifier within the boundary.
    /// </summary>
    public long OperationId { get; }

    /// <summary>
    /// Gets the logical operation kind.
    /// </summary>
    public StorageOperationKind Kind { get; }

    /// <summary>
    /// Gets the affected key when the operation targets one key.
    /// </summary>
    public string? Key { get; }

    /// <summary>
    /// Gets the stable logical storage name.
    /// </summary>
    public string StorageName { get; }

    internal bool BelongsTo(SimulationStorageBoundary boundary)
    {
        return ReferenceEquals(_owner, boundary);
    }

    internal void MarkTerminal()
    {
        if (Interlocked.CompareExchange(ref _terminal, 1, 0) != 0)
        {
            throw new InvalidOperationException($"Storage operation lease {OperationId} has already reached a terminal state.");
        }
    }
}
