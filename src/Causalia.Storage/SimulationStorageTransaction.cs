using Causalia.Storage.Internal;

namespace Causalia.Storage;

/// <summary>
/// Stages deterministic writes and atomically applies them at the simulated commit boundary.
/// </summary>
public sealed class SimulationStorageTransaction
{
    private readonly SimulationStorageBoundary _boundary;
    private readonly SimulationStorageDatabase _database;
    private readonly Dictionary<string, StagedStorageChange> _changes = new(StringComparer.Ordinal);
    private bool _completed;
    private bool _commitInProgress;

    internal SimulationStorageTransaction(SimulationStorageDatabase database, SimulationStorageBoundary boundary)
    {
        _database = database;
        _boundary = boundary;
    }

    /// <summary>
    /// Stages a write and optionally requires the durable version to match at commit time.
    /// </summary>
    public void Write(string key, ReadOnlyMemory<byte> value, long? expectedVersion = null)
    {
        ThrowIfCompleted();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _changes[key] = new StagedStorageChange(value.ToArray(), expectedVersion);
    }

    /// <summary>
    /// Stages a delete and optionally requires the durable version to match at commit time.
    /// </summary>
    public void Delete(string key, long? expectedVersion = null)
    {
        ThrowIfCompleted();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _changes[key] = new StagedStorageChange(null, expectedVersion);
    }

    /// <summary>
    /// Atomically applies all staged changes through the deterministic commit boundary.
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        ThrowIfCompleted();
        _commitInProgress = true;

        try
        {
            var lease = await _boundary.BeginAsync(StorageOperationKind.Commit, null, cancellationToken);

            try
            {
                _database.Commit(_changes);
                _completed = true;
            }
            catch (Exception exception)
            {
                _boundary.ProviderFailed(lease, exception);
                throw;
            }

            await _boundary.CompleteAsync(lease, cancellationToken);
        }
        finally
        {
            _commitInProgress = false;
        }
    }

    /// <summary>
    /// Discards all staged changes.
    /// </summary>
    public void Rollback()
    {
        ThrowIfCompleted();
        _changes.Clear();
        _completed = true;
    }

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The storage transaction has already completed.");
        }

        if (_commitInProgress)
        {
            throw new InvalidOperationException("The storage transaction is already committing.");
        }
    }
}
