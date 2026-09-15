using System.Globalization;
using System.Text.Json;
using Causalia.Storage;
using Causalia.Storage.Exceptions;

namespace Causalia.Dapr;

/// <summary>
/// Simulates one Dapr state-store component using deterministic durable Causalia storage.
/// </summary>
public sealed class SimulationDaprStateStore
{
    private readonly SimulationStorageClient _client;

    internal SimulationDaprStateStore(
        string name,
        SimulationStorageDatabase database,
        SimulationStorageClientOptions? options)
    {
        Name = name;
        _client = database.CreateClient(options: options);
    }

    /// <summary>
    /// Gets the logical Dapr state-store component name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Reads one typed state value and its ETag.
    /// </summary>
    public async Task<DaprStateValue<T>> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var result = await _client.ReadAsync(key, cancellationToken);

        if (!result.Exists)
        {
            return new DaprStateValue<T>(false, default, null);
        }

        var value = JsonSerializer.Deserialize<T>(result.Value.Span);
        return new DaprStateValue<T>(true, value, ToETag(result.Version));
    }

    /// <summary>
    /// Saves one typed state value without an ETag precondition.
    /// </summary>
    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        return _client.WriteAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), cancellationToken);
    }

    /// <summary>
    /// Saves one typed state value only when its ETag matches the current version.
    /// </summary>
    public async Task<bool> TrySaveAsync<T>(string key, T value, string etag, CancellationToken cancellationToken)
    {
        try
        {
            await _client.WriteAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), ParseETag(etag), cancellationToken);
            return true;
        }
        catch (SimulationStorageConcurrencyException)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes one state value without an ETag precondition.
    /// </summary>
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken)
    {
        return _client.DeleteAsync(key, cancellationToken);
    }

    /// <summary>
    /// Deletes one state value only when its ETag matches the current version.
    /// </summary>
    public async Task<bool> TryDeleteAsync(string key, string etag, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.DeleteAsync(key, ParseETag(etag), cancellationToken);
        }
        catch (SimulationStorageConcurrencyException)
        {
            return false;
        }
    }

    /// <summary>
    /// Atomically applies multiple state upserts and deletes.
    /// </summary>
    public async Task ExecuteTransactionAsync(
        IReadOnlyList<DaprStateOperation> operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var transaction = _client.BeginTransaction();

        foreach (var operation in operations)
        {
            if (operation.Value.HasValue)
            {
                transaction.Write(operation.Key, operation.Value.Value, ParseOptionalETag(operation.ETag));
            }
            else
            {
                transaction.Delete(operation.Key, ParseOptionalETag(operation.ETag));
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static string ToETag(long version)
    {
        return version.ToString(CultureInfo.InvariantCulture);
    }

    private static long ParseETag(string etag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etag);
        return long.Parse(etag, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static long? ParseOptionalETag(string? etag)
    {
        return string.IsNullOrWhiteSpace(etag) ? null : ParseETag(etag);
    }
}
