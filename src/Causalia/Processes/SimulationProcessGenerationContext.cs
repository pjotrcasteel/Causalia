using Causalia.Nodes;

namespace Causalia.Processes;

/// <summary>
/// Exposes deterministic services scoped to one volatile process generation.
/// </summary>
public sealed class SimulationProcessGenerationContext
{
    private readonly List<Task> _backgroundOperations = [];
    private readonly SimulationContext _context;

    internal SimulationProcessGenerationContext(
        SimulationContext context,
        SimulationNode node,
        CancellationToken cancellationToken)
    {
        _context = context;
        Node = node;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the owning simulation context.
    /// </summary>
    public SimulationContext Simulation => _context;

    /// <summary>
    /// Gets the stable low-level node that owns this process.
    /// </summary>
    public SimulationNode Node { get; }

    /// <summary>
    /// Gets the current process generation number.
    /// </summary>
    public int Generation => Node.Generation;

    /// <summary>
    /// Gets the token cancelled when this process generation stops, crashes, or the simulation ends.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Starts deterministic background work scoped to this generation.
    /// </summary>
    public Task RunBackgroundAsync(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        CancellationToken.ThrowIfCancellationRequested();
        var task = RunBackgroundCoreAsync(operation);
        _backgroundOperations.Add(task);
        _context.TrackOperation(task);
        return task;
    }

    internal async Task WaitForBackgroundOperationsAsync()
    {
        if (_backgroundOperations.Count == 0)
        {
            return;
        }

        await Task.WhenAll(_backgroundOperations);
    }

    private async Task RunBackgroundCoreAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await operation(CancellationToken);
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
        }
    }
}
