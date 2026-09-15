using Causalia.Nodes;

namespace Causalia.Processes;

/// <summary>
/// Owns a restartable volatile process generation while preserving a stable logical node identity.
/// </summary>
public sealed class SimulationProcess<TGeneration> : IAsyncDisposable
    where TGeneration : class, ISimulationProcessGeneration
{
    private readonly SimulationContext _context;
    private readonly Func<SimulationProcessGenerationContext, CancellationToken, Task<TGeneration>> _generationFactory;
    private readonly SimulationProcessOptions _options;
    private CancellationTokenSource? _generationCancellation;
    private SimulationProcessGenerationContext? _generationContext;
    private TGeneration? _current;
    private bool _disposed;
    private TaskCompletionSource<bool> _runningSignal = CreateSignal();

    private SimulationProcess(
        SimulationContext context,
        SimulationProcessOptions options,
        Func<SimulationProcessGenerationContext, CancellationToken, Task<TGeneration>> generationFactory,
        SimulationNode node)
    {
        _context = context;
        _options = options;
        _generationFactory = generationFactory;
        Node = node;
        State = SimulationProcessState.Starting;
    }

    /// <summary>
    /// Gets the stable logical process name.
    /// </summary>
    public string Name => _options.Name;

    /// <summary>
    /// Gets the stable node used by every process generation.
    /// </summary>
    public SimulationNode Node { get; }

    /// <summary>
    /// Gets the current process generation number.
    /// </summary>
    public int Generation => Node.Generation;

    /// <summary>
    /// Gets the current process lifecycle state.
    /// </summary>
    public SimulationProcessState State { get; private set; }

    /// <summary>
    /// Gets whether the process currently has a running generation.
    /// </summary>
    public bool IsRunning => State == SimulationProcessState.Running;

    /// <summary>
    /// Gets the current volatile generation instance.
    /// </summary>
    public TGeneration Current
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return IsRunning && _current is not null
                ? _current
                : throw new InvalidOperationException($"Process '{Name}' does not currently have a running generation.");
        }
    }

    /// <summary>
    /// Waits until a process generation is running.
    /// </summary>
    public Task WaitUntilRunningAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return IsRunning ? Task.CompletedTask : _runningSignal.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Executes work against the current generation and cancels it when that generation terminates.
    /// </summary>
    public async Task RunAsync(
        Func<TGeneration, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await WaitUntilRunningAsync(cancellationToken);
        var generation = Current;
        var generationCancellation = _generationCancellation
            ?? throw new InvalidOperationException($"Process '{Name}' has no generation cancellation source.");

        await Node.RunAsync(
            async nodeCancellationToken =>
            {
                using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    nodeCancellationToken,
                    generationCancellation.Token);
                await operation(generation, executionCancellation.Token);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gracefully stops the current generation and releases all of its volatile state.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (State == SimulationProcessState.Stopped)
        {
            return;
        }

        if (State != SimulationProcessState.Running)
        {
            throw new InvalidOperationException($"Process '{Name}' cannot be stopped from state '{State}'.");
        }

        State = SimulationProcessState.Stopping;
        ResetRunningSignal();
        Trace($"process:stopping:{Name}:generation:{Generation}");
        _generationCancellation!.Cancel();

        try
        {
            await _current!.StopAsync(cancellationToken);
            await _generationContext!.WaitForBackgroundOperationsAsync();
        }
        finally
        {
            Node.Stop();
            await DisposeCurrentGenerationAsync();
            State = SimulationProcessState.Stopped;
            Trace($"process:stopped:{Name}:generation:{Generation}");
        }
    }

    /// <summary>
    /// Abruptly crashes the current generation without invoking its graceful StopAsync hook.
    /// </summary>
    public async Task CrashAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (State == SimulationProcessState.Crashed)
        {
            return;
        }

        if (State != SimulationProcessState.Running)
        {
            throw new InvalidOperationException($"Process '{Name}' cannot crash from state '{State}'.");
        }

        ResetRunningSignal();
        Trace($"process:crashing:{Name}:generation:{Generation}");
        _generationCancellation!.Cancel();
        Node.Crash();
        State = SimulationProcessState.Crashed;

        try
        {
            await _generationContext!.WaitForBackgroundOperationsAsync();
        }
        finally
        {
            await DisposeCurrentGenerationAsync();
            Trace($"process:crashed:{Name}:generation:{Generation}");
        }
    }

    /// <summary>
    /// Starts a fresh volatile generation after a graceful stop or crash.
    /// </summary>
    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (State is not SimulationProcessState.Stopped and not SimulationProcessState.Crashed)
        {
            throw new InvalidOperationException($"Process '{Name}' cannot restart from state '{State}'.");
        }

        State = SimulationProcessState.Restarting;
        Trace($"process:restarting:{Name}:from-generation:{Generation}");
        Node.Restart();

        try
        {
            await StartGenerationAsync(cancellationToken);
        }
        catch
        {
            await AbortFailedGenerationAsync();
            throw;
        }
    }

    /// <summary>
    /// Gracefully stops and disposes this simulated process.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (State == SimulationProcessState.Running)
        {
            await StopAsync(_context.CancellationToken);
        }
        else
        {
            await DisposeCurrentGenerationAsync();
        }

        _disposed = true;
        _generationCancellation?.Dispose();
        _generationCancellation = null;
    }

    internal static async Task<SimulationProcess<TGeneration>> StartAsync(
        SimulationContext context,
        SimulationProcessOptions options,
        Func<SimulationProcessGenerationContext, CancellationToken, Task<TGeneration>> generationFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(generationFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        cancellationToken.ThrowIfCancellationRequested();
        var node = context.CreateNode(options.Name);
        var process = new SimulationProcess<TGeneration>(context, options, generationFactory, node);
        process.Trace($"process:created:{options.Name}:generation:{node.Generation}");

        try
        {
            await process.StartGenerationAsync(cancellationToken);
            return process;
        }
        catch
        {
            await process.AbortFailedGenerationAsync();
            throw;
        }
    }

    private async Task StartGenerationAsync(CancellationToken cancellationToken)
    {
        _generationCancellation?.Dispose();
        _generationCancellation = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken);
        _generationContext = new SimulationProcessGenerationContext(_context, Node, _generationCancellation.Token);
        Trace($"process:generation:creating:{Name}:generation:{Generation}");
        _current = await _generationFactory(_generationContext, cancellationToken);
        ArgumentNullException.ThrowIfNull(_current);
        Trace($"process:generation:starting:{Name}:generation:{Generation}");
        await _current.StartAsync(cancellationToken);
        State = SimulationProcessState.Running;
        _runningSignal.TrySetResult(true);
        Trace($"process:running:{Name}:generation:{Generation}");
    }

    private async Task AbortFailedGenerationAsync()
    {
        _generationCancellation?.Cancel();
        Node.Crash();
        State = SimulationProcessState.Crashed;

        if (_generationContext is not null)
        {
            await _generationContext.WaitForBackgroundOperationsAsync();
        }

        await DisposeCurrentGenerationAsync();
    }

    private async Task DisposeCurrentGenerationAsync()
    {
        if (_current is not null)
        {
            await _current.DisposeAsync();
            _current = null;
        }

        _generationContext = null;
    }

    private void ResetRunningSignal()
    {
        if (_runningSignal.Task.IsCompleted)
        {
            _runningSignal = CreateSignal();
        }
    }

    private void Trace(string message)
    {
        if (_options.TraceLifecycle)
        {
            _context.TraceEvent(message);
        }
    }

    private static TaskCompletionSource<bool> CreateSignal()
    {
        return new TaskCompletionSource<bool>();
    }
}
