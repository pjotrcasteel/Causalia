using System.Text;
using Causalia;
using Causalia.ExampleApp;
using Causalia.Storage;
using Causalia.Storage.Exceptions;

namespace Causalia.Examples.Tests;

/// <summary>
/// Maps the application storage port to deterministic transactional storage.
/// </summary>
[DeterministicSimulation]
public sealed class SimulatedOrderStore : IOrderStore
{
    private readonly SimulationStorageClient _client;

    /// <summary>
    /// Initializes the adapter for a simulation-scoped client.
    /// </summary>
    public SimulatedOrderStore(SimulationStorageClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken)
    {
        try
        {
            return (await _client.ReadAsync(orderId, cancellationToken)).Exists;
        }
        catch (SimulationStorageTransientException exception)
        {
            throw new IOException("Storage temporarily unavailable.", exception);
        }
    }

    /// <inheritdoc />
    public async Task CreateIfMissingAsync(string orderId, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = _client.BeginTransaction();
            transaction.Write(orderId, Encoding.UTF8.GetBytes("accepted"), expectedVersion: 0);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SimulationStorageConcurrencyException)
        {
            // A competing acceptance already created the same marker atomically.
        }
        catch (SimulationStorageTransientException exception)
        {
            throw new IOException("Storage temporarily unavailable.", exception);
        }
        catch (SimulationStorageAmbiguousCommitException exception)
        {
            throw new IOException("Storage response lost; the write may have committed.", exception);
        }
    }
}
