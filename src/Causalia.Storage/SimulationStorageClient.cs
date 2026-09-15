using Causalia.Nodes;

namespace Causalia.Storage;

/// <summary>
/// Provides deterministic node-facing access to a durable simulated database.
/// </summary>
public sealed class SimulationStorageClient
{
    private readonly SimulationStorageDatabase _database;

    internal SimulationStorageClient(
        SimulationStorageDatabase database,
        SimulationContext context,
        SimulationNode? node,
        SimulationStorageClientOptions options)
    {
        _database = database;
        Boundary = new SimulationStorageBoundary(context, database.Name, node, options);
    }

    /// <summary>
    /// Gets the deterministic boundary used by this client.
    /// </summary>
    public SimulationStorageBoundary Boundary { get; }

    /// <summary>
    /// Reads one versioned value.
    /// </summary>
    public Task<StorageReadResult> ReadAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Boundary.ExecuteAsync(StorageOperationKind.Read, key, () => _database.Read(key), cancellationToken);
    }

    /// <summary>
    /// Writes one value without an expected-version check.
    /// </summary>
    public Task WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
    {
        return WriteAsync(key, value, null, cancellationToken);
    }

    /// <summary>
    /// Writes one value and optionally requires the current version to match.
    /// </summary>
    public Task WriteAsync(string key, ReadOnlyMemory<byte> value, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Boundary.ExecuteAsync(StorageOperationKind.Write, key, () => _database.Write(key, value, expectedVersion), cancellationToken);
    }

    /// <summary>
    /// Deletes one value without an expected-version check.
    /// </summary>
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken)
    {
        return DeleteAsync(key, null, cancellationToken);
    }

    /// <summary>
    /// Deletes one value and optionally requires the current version to match.
    /// </summary>
    public Task<bool> DeleteAsync(string key, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Boundary.ExecuteAsync(StorageOperationKind.Delete, key, () => _database.Delete(key, expectedVersion), cancellationToken);
    }

    /// <summary>
    /// Starts a transaction that stages changes until commit.
    /// </summary>
    public SimulationStorageTransaction BeginTransaction()
    {
        return new SimulationStorageTransaction(_database, Boundary);
    }
}
