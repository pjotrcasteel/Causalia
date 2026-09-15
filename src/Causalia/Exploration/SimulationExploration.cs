using Causalia.Runtime;

namespace Causalia.Exploration;

/// <summary>
/// Declares logical operation dependencies used by advanced schedule exploration.
/// </summary>
public sealed class SimulationExploration
{
    private readonly DeterministicScheduler _scheduler;

    internal SimulationExploration(DeterministicScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>
    /// Declares a read-only access to a logical shared resource.
    /// </summary>
    public ExplorationResourceAccess Read(string resource)
    {
        return CreateAccess(resource, ExplorationAccessKind.Read);
    }

    /// <summary>
    /// Declares a mutating access to a logical shared resource.
    /// </summary>
    public ExplorationResourceAccess Write(string resource)
    {
        return CreateAccess(resource, ExplorationAccessKind.Write);
    }

    /// <summary>
    /// Declares synchronization through a logical shared resource.
    /// </summary>
    public ExplorationResourceAccess Synchronize(string resource)
    {
        return CreateAccess(resource, ExplorationAccessKind.Synchronize);
    }

    /// <summary>
    /// Runs one logical operation whose complete shared-resource dependency profile is described by <paramref name="accesses"/>.
    /// </summary>
    public async Task RunAsync(
        string name,
        IReadOnlyList<ExplorationResourceAccess> accesses,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(accesses);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        if (accesses.Any(access => access is null))
        {
            throw new ArgumentException("Exploration resource accesses cannot contain null values.", nameof(accesses));
        }

        var operationId = _scheduler.RegisterExplorationOperation(name, accesses);
        var task = _scheduler.StartExplorationOperation(operationId, operation, cancellationToken);
        await task;
    }

    private static ExplorationResourceAccess CreateAccess(string resource, ExplorationAccessKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        return new ExplorationResourceAccess(resource, kind);
    }
}
