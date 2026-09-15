using Causalia.Nodes;

namespace Causalia.Storage;

/// <summary>
/// Adds deterministic durable-storage primitives to a simulation context.
/// </summary>
public static class SimulationContextStorageExtensions
{
    /// <summary>
    /// Creates a durable versioned database whose committed state survives simulated node restarts.
    /// </summary>
    public static SimulationStorageDatabase CreateStorageDatabase(this SimulationContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new SimulationStorageDatabase(context, name);
    }

    /// <summary>
    /// Creates a deterministic storage-operation boundary for an external persistence adapter.
    /// </summary>
    public static SimulationStorageBoundary CreateStorageBoundary(
        this SimulationContext context,
        string name,
        SimulationNode? node = null,
        SimulationStorageClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (node is not null && !node.BelongsTo(context))
        {
            throw new InvalidOperationException("The storage boundary node must belong to the same simulation context.");
        }

        return new SimulationStorageBoundary(context, name, node, options ?? new SimulationStorageClientOptions());
    }
}
