using Causalia.Nodes;
using Causalia.Storage.Exceptions;
using Causalia.Storage.Internal;

namespace Causalia.Storage;

/// <summary>
/// Represents durable versioned state shared by one or more deterministic storage clients.
/// </summary>
public sealed class SimulationStorageDatabase
{
    private readonly Dictionary<string, StoredValue> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastVersions = new(StringComparer.Ordinal);
    private readonly SimulationContext _context;

    internal SimulationStorageDatabase(SimulationContext context, string name)
    {
        _context = context;
        Name = name;
        _context.TraceEvent($"storage:{Name}:database:created");
    }

    /// <summary>
    /// Gets the stable logical database name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a node-bound client over the same durable database state.
    /// </summary>
    public SimulationStorageClient CreateClient(SimulationNode? node = null, SimulationStorageClientOptions? options = null)
    {
        if (node is not null && !node.BelongsTo(_context))
        {
            throw new InvalidOperationException("The storage client node must belong to the same simulation context as the database.");
        }

        return new SimulationStorageClient(this, _context, node, options ?? new SimulationStorageClientOptions());
    }

    internal StorageReadResult Read(string key)
    {
        if (!_values.TryGetValue(key, out var stored))
        {
            return new StorageReadResult(false, ReadOnlyMemory<byte>.Empty, 0);
        }

        return new StorageReadResult(true, stored.Value.ToArray(), stored.Version);
    }

    internal void Write(string key, ReadOnlyMemory<byte> value, long? expectedVersion)
    {
        var actualVersion = _values.TryGetValue(key, out var existing) ? existing.Version : 0;
        ValidateExpectedVersion(key, expectedVersion, actualVersion);
        var nextVersion = GetNextVersion(key);
        _values[key] = new StoredValue(value.ToArray(), nextVersion);
        _lastVersions[key] = nextVersion;
    }

    internal bool Delete(string key, long? expectedVersion)
    {
        var exists = _values.TryGetValue(key, out var existing);
        var actualVersion = exists ? existing!.Version : 0;
        ValidateExpectedVersion(key, expectedVersion, actualVersion);
        return exists && _values.Remove(key);
    }

    internal void Commit(IReadOnlyDictionary<string, StagedStorageChange> changes)
    {
        var prepared = new Dictionary<string, StoredValue?>(StringComparer.Ordinal);

        foreach (var change in changes)
        {
            var actualVersion = _values.TryGetValue(change.Key, out var existing) ? existing.Version : 0;
            ValidateExpectedVersion(change.Key, change.Value.ExpectedVersion, actualVersion);
            prepared.Add(change.Key, change.Value.Value is null
                ? null
                : new StoredValue(change.Value.Value.ToArray(), GetNextVersion(change.Key)));
        }

        foreach (var change in prepared)
        {
            if (change.Value is null)
            {
                _values.Remove(change.Key);
                continue;
            }

            _values[change.Key] = change.Value;
            _lastVersions[change.Key] = change.Value.Version;
        }
    }

    private long GetNextVersion(string key)
    {
        // Retain the previous revision across deletion so a stale CAS/ETag cannot match a recreated key.
        return checked(_lastVersions.GetValueOrDefault(key) + 1);
    }

    private static void ValidateExpectedVersion(string key, long? expectedVersion, long actualVersion)
    {
        if (expectedVersion.HasValue && expectedVersion.Value != actualVersion)
        {
            throw new SimulationStorageConcurrencyException(key, expectedVersion.Value, actualVersion);
        }
    }
}
