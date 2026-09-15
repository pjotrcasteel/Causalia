using System.Net.Http.Json;
using Causalia.AspNetCore.Networking;

namespace Causalia.Dapr;

/// <summary>
/// Provides app-scoped access to deterministic Dapr building blocks.
/// </summary>
public sealed class SimulationDaprClient
{
    private readonly SimulationDaprEnvironment _environment;
    private readonly SimulationHttpNetwork? _network;

    internal SimulationDaprClient(string appId, SimulationDaprEnvironment environment, SimulationHttpNetwork? network)
    {
        AppId = appId;
        _environment = environment;
        _network = network;
    }

    /// <summary>
    /// Gets the logical source application identifier.
    /// </summary>
    public string AppId { get; }

    /// <summary>
    /// Gets a typed state value and its ETag.
    /// </summary>
    public Task<DaprStateValue<T>> GetStateAsync<T>(
        string storeName,
        string key,
        CancellationToken cancellationToken)
    {
        return _environment.ResolveStateStore(storeName).GetAsync<T>(key, cancellationToken);
    }

    /// <summary>
    /// Saves a typed state value.
    /// </summary>
    public Task SaveStateAsync<T>(
        string storeName,
        string key,
        T value,
        CancellationToken cancellationToken)
    {
        return _environment.ResolveStateStore(storeName).SaveAsync(key, value, cancellationToken);
    }

    /// <summary>
    /// Saves state only when the supplied ETag matches.
    /// </summary>
    public Task<bool> TrySaveStateAsync<T>(
        string storeName,
        string key,
        T value,
        string etag,
        CancellationToken cancellationToken)
    {
        return _environment.ResolveStateStore(storeName).TrySaveAsync(key, value, etag, cancellationToken);
    }

    /// <summary>
    /// Deletes one state key.
    /// </summary>
    public Task<bool> DeleteStateAsync(string storeName, string key, CancellationToken cancellationToken)
    {
        return _environment.ResolveStateStore(storeName).DeleteAsync(key, cancellationToken);
    }

    /// <summary>
    /// Deletes one state key only when the supplied ETag matches.
    /// </summary>
    public Task<bool> TryDeleteStateAsync(
        string storeName,
        string key,
        string etag,
        CancellationToken cancellationToken)
    {
        return _environment.ResolveStateStore(storeName).TryDeleteAsync(key, etag, cancellationToken);
    }

    /// <summary>
    /// Executes one atomic Dapr state transaction.
    /// </summary>
    public Task ExecuteStateTransactionAsync(
        string storeName,
        IReadOnlyList<DaprStateOperation> operations,
        CancellationToken cancellationToken)
    {
        return _environment.ResolveStateStore(storeName).ExecuteTransactionAsync(operations, cancellationToken);
    }

    /// <summary>
    /// Publishes one typed event to a simulated Dapr pub/sub component.
    /// </summary>
    public Task PublishEventAsync<T>(
        string pubSubName,
        string topic,
        T data,
        CancellationToken cancellationToken)
    {
        return _environment.ResolvePubSub(pubSubName).PublishAsync(topic, data, cancellationToken);
    }

    /// <summary>
    /// Invokes another registered application over Causalia's deterministic HTTP network.
    /// </summary>
    public async Task<TResponse?> InvokeMethodAsync<TRequest, TResponse>(
        string destinationAppId,
        HttpMethod method,
        string path,
        TRequest? request,
        CancellationToken cancellationToken)
    {
        if (_network is null)
        {
            throw new InvalidOperationException("This Dapr environment was created without a SimulationHttpNetwork.");
        }

        using var client = _network.CreateClient(AppId, destinationAppId);
        using var message = new HttpRequestMessage(method, path);

        if (request is not null)
        {
            message.Content = JsonContent.Create(request);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
    }
}
