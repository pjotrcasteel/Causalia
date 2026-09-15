namespace Causalia.Nodes;

/// <summary>
/// Represents one deterministic process or service instance inside a simulation.
/// </summary>
public sealed class SimulationNode
{
    private readonly SimulationContext _context;
    private CancellationTokenSource _generationCancellation;
    private TaskCompletionSource<bool> _runningSignal = CreateCompletedSignal();

    internal SimulationNode(SimulationContext context, string name)
    {
        _context = context;
        Name = name;
        _generationCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        State = SimulationNodeState.Running;
        Generation = 1;
        _context.TraceEvent($"node:created:{Name}:generation:{Generation}");
    }

    /// <summary>
    /// Gets the stable logical name of the node.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the current lifecycle state.
    /// </summary>
    public SimulationNodeState State { get; private set; }

    /// <summary>
    /// Gets the current node generation. Restarting a crashed node increments this value.
    /// </summary>
    public int Generation { get; private set; }

    /// <summary>
    /// Gets whether the node is currently running.
    /// </summary>
    public bool IsRunning => State == SimulationNodeState.Running;

    /// <summary>
    /// Crashes the node and cancels work associated with the current generation.
    /// </summary>
    public bool Crash()
    {
        if (State == SimulationNodeState.Crashed)
        {
            return false;
        }

        if (State == SimulationNodeState.Running)
        {
            _runningSignal = new TaskCompletionSource<bool>();
        }

        State = SimulationNodeState.Crashed;
        _context.TraceEvent($"node:crashed:{Name}:generation:{Generation}");
        _generationCancellation.Cancel();
        return true;
    }

    /// <summary>
    /// Gracefully marks the node as stopped and cancels work associated with the current generation.
    /// </summary>
    public bool Stop()
    {
        if (State != SimulationNodeState.Running)
        {
            return false;
        }

        State = SimulationNodeState.Stopped;
        _runningSignal = new TaskCompletionSource<bool>();
        _context.TraceEvent($"node:stopped:{Name}:generation:{Generation}");
        _generationCancellation.Cancel();
        return true;
    }

    /// <summary>
    /// Restarts a stopped or crashed node as a new generation.
    /// </summary>
    public bool Restart()
    {
        if (State == SimulationNodeState.Running)
        {
            return false;
        }

        _generationCancellation.Dispose();
        _generationCancellation = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken);
        Generation = checked(Generation + 1);
        State = SimulationNodeState.Running;
        _context.TraceEvent($"node:restarted:{Name}:generation:{Generation}");
        _runningSignal.TrySetResult(true);
        return true;
    }

    /// <summary>
    /// Waits until the node is running.
    /// </summary>
    public Task WaitUntilRunningAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsRunning ? Task.CompletedTask : _runningSignal.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Executes work in the current node generation so a crash cancels the supplied operation token.
    /// </summary>
    public async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await WaitUntilRunningAsync(cancellationToken);
        using var executionCancellation = CreateExecutionCancellationSource(cancellationToken);
        await operation(executionCancellation.Token);
    }

    internal bool BelongsTo(SimulationContext context)
    {
        return ReferenceEquals(_context, context);
    }

    internal CancellationTokenSource CreateExecutionCancellationSource(CancellationToken cancellationToken)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _generationCancellation.Token);
    }

    internal void Dispose()
    {
        _runningSignal.TrySetCanceled();

        if (!_generationCancellation.IsCancellationRequested)
        {
            _generationCancellation.Cancel();
        }

        _generationCancellation.Dispose();
    }

    private static TaskCompletionSource<bool> CreateCompletedSignal()
    {
        var signal = new TaskCompletionSource<bool>();
        signal.SetResult(true);
        return signal;
    }
}
