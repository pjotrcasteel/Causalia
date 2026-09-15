using Causalia.Storage.Faults;

namespace Causalia.Storage;

/// <summary>
/// Configures one node-facing client for deterministic durable storage.
/// </summary>
public sealed class SimulationStorageClientOptions
{
    /// <summary>
    /// Gets the virtual latency applied before read, write and delete operations.
    /// </summary>
    public TimeSpan OperationLatency { get; init; }

    /// <summary>
    /// Gets the virtual latency applied before commit operations.
    /// </summary>
    public TimeSpan CommitLatency { get; init; }

    /// <summary>
    /// Gets the deterministic storage fault plan.
    /// </summary>
    public StorageFaultPlan? Faults { get; init; }
}
