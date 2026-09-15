using Causalia.AspNetCore.Networking;
using Causalia.Storage;

namespace Causalia.Dapr;

/// <summary>
/// Hosts deterministic Dapr building-block components inside one simulation world.
/// </summary>
public sealed class SimulationDaprEnvironment
{
    private readonly Dictionary<string, SimulationDaprPubSub> _pubSubs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimulationDaprStateStore> _stateStores = new(StringComparer.Ordinal);
    private readonly SimulationContext _context;
    private readonly SimulationHttpNetwork? _network;

    internal SimulationDaprEnvironment(SimulationContext context, SimulationHttpNetwork? network)
    {
        _context = context;
        _network = network;
        _context.TraceEvent("dapr:environment:created");
    }

    /// <summary>
    /// Adds a durable Dapr state-store component.
    /// </summary>
    public SimulationDaprStateStore AddStateStore(string name, SimulationStorageClientOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_stateStores.ContainsKey(name))
        {
            throw new InvalidOperationException($"A Dapr state store named '{name}' already exists.");
        }

        SimulationStorageDatabase database = _context.CreateStorageDatabase($"dapr-{name}");
        var store = new SimulationDaprStateStore(name, database, options);
        _stateStores.Add(name, store);
        _context.TraceEvent($"dapr:state:created:{name}");
        return store;
    }

    /// <summary>
    /// Adds a deterministic Dapr pub/sub component.
    /// </summary>
    public SimulationDaprPubSub AddPubSub(string name, DaprPubSubOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_pubSubs.ContainsKey(name))
        {
            throw new InvalidOperationException($"A Dapr pub/sub component named '{name}' already exists.");
        }

        var pubSub = new SimulationDaprPubSub(_context, name, options ?? new DaprPubSubOptions());
        _pubSubs.Add(name, pubSub);
        return pubSub;
    }

    /// <summary>
    /// Creates an app-scoped Dapr client facade over the configured simulated components.
    /// </summary>
    public SimulationDaprClient CreateClient(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return new SimulationDaprClient(appId, this, _network);
    }

    internal SimulationDaprStateStore ResolveStateStore(string name)
    {
        return _stateStores.TryGetValue(name, out var store)
            ? store
            : throw new InvalidOperationException($"Dapr state store '{name}' is not configured.");
    }

    internal SimulationDaprPubSub ResolvePubSub(string name)
    {
        return _pubSubs.TryGetValue(name, out var pubSub)
            ? pubSub
            : throw new InvalidOperationException($"Dapr pub/sub component '{name}' is not configured.");
    }
}
