using Causalia.Faults;
using Causalia.Nodes;
using Causalia.Storage.Exceptions;
using Causalia.Storage.Faults;

namespace Causalia.Storage;

/// <summary>
/// Applies deterministic latency, node lifecycle and fault injection around durable-storage operations.
/// </summary>
public sealed class SimulationStorageBoundary
{
    private readonly FaultInjector<StorageOperationContext, StorageFault>? _faultInjector;
    private readonly SimulationContext _context;
    private readonly SimulationNode? _node;
    private readonly SimulationStorageClientOptions _options;
    private long _nextOperationId;

    internal SimulationStorageBoundary(
        SimulationContext context,
        string storageName,
        SimulationNode? node,
        SimulationStorageClientOptions options)
    {
        _context = context;
        _node = node;
        _options = options;
        StorageName = storageName;

        if (options.OperationLatency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.OperationLatency, "Operation latency cannot be negative.");
        }

        if (options.CommitLatency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.CommitLatency, "Commit latency cannot be negative.");
        }

        if (options.Faults is not null)
        {
            _faultInjector = context.CreateFaultInjector($"storage:{storageName}", options.Faults.Plan);
        }
    }

    /// <summary>
    /// Gets the stable logical storage name used in trace and failure metadata.
    /// </summary>
    public string StorageName { get; }

    /// <summary>
    /// Opens the before-boundary for one storage operation and returns a lease used to complete it.
    /// </summary>
    public async Task<StorageOperationLease> BeginAsync(
        StorageOperationKind kind,
        string? key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await WaitForNodeAsync(cancellationToken);
        var lease = new StorageOperationLease(this, checked(++_nextOperationId), kind, key);
        var phase = GetBeforePhase(kind);
        _context.TraceEvent($"storage:{StorageName}:operation:{lease.OperationId}:begin:{kind}:{FormatKey(key)}");
        await ApplyBoundaryAsync(lease, phase, GetBaseLatency(kind), cancellationToken);
        return lease;
    }

    /// <summary>
    /// Completes the after-boundary for one storage operation.
    /// </summary>
    public async Task CompleteAsync(StorageOperationLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        EnsureLeaseOwnership(lease);
        cancellationToken.ThrowIfCancellationRequested();
        lease.MarkTerminal();
        await ApplyBoundaryAsync(lease, GetAfterPhase(lease.Kind), TimeSpan.Zero, cancellationToken);
        _context.TraceEvent($"storage:{StorageName}:operation:{lease.OperationId}:completed:{lease.Kind}:{FormatKey(lease.Key)}");
    }

    /// <summary>
    /// Records that the backing provider failed between the before and after boundaries.
    /// </summary>
    public void ProviderFailed(StorageOperationLease lease, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(lease);
        EnsureLeaseOwnership(lease);
        ArgumentNullException.ThrowIfNull(exception);
        lease.MarkTerminal();
        _context.TraceEvent(
            $"storage:{StorageName}:operation:{lease.OperationId}:provider-failed:{lease.Kind}:{exception.GetType().Name}");
    }

    /// <summary>
    /// Records that the backing provider cancelled between the before and after boundaries.
    /// </summary>
    public void ProviderCanceled(StorageOperationLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        EnsureLeaseOwnership(lease);
        lease.MarkTerminal();
        _context.TraceEvent(
            $"storage:{StorageName}:operation:{lease.OperationId}:provider-cancelled:{lease.Kind}:" +
            $"token-cancelled:{cancellationToken.IsCancellationRequested}");
    }

    internal async Task<T> ExecuteAsync<T>(
        StorageOperationKind kind,
        string? key,
        Func<T> operation,
        CancellationToken cancellationToken)
    {
        var lease = await BeginAsync(kind, key, cancellationToken);
        using var linkedCancellation = CreateExecutionCancellationSource(cancellationToken);
        linkedCancellation.Token.ThrowIfCancellationRequested();
        T result;

        try
        {
            result = operation();
        }
        catch (Exception exception)
        {
            ProviderFailed(lease, exception);
            throw;
        }

        await CompleteAsync(lease, linkedCancellation.Token);
        return result;
    }

    internal async Task ExecuteAsync(
        StorageOperationKind kind,
        string? key,
        Action operation,
        CancellationToken cancellationToken)
    {
        var lease = await BeginAsync(kind, key, cancellationToken);
        using var linkedCancellation = CreateExecutionCancellationSource(cancellationToken);
        linkedCancellation.Token.ThrowIfCancellationRequested();

        try
        {
            operation();
        }
        catch (Exception exception)
        {
            ProviderFailed(lease, exception);
            throw;
        }

        await CompleteAsync(lease, linkedCancellation.Token);
    }

    private async Task ApplyBoundaryAsync(
        StorageOperationLease lease,
        StorageOperationPhase phase,
        TimeSpan baseLatency,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation = CreateExecutionCancellationSource(cancellationToken);
        var token = linkedCancellation.Token;
        token.ThrowIfCancellationRequested();

        if (baseLatency > TimeSpan.Zero)
        {
            _context.TraceEvent(
                $"storage:{StorageName}:operation:{lease.OperationId}:latency:{phase}:{baseLatency.Ticks}");
            await Task.Delay(baseLatency, _context.TimeProvider, token);
        }

        if (_faultInjector is null)
        {
            return;
        }

        var operationContext = new StorageOperationContext(lease.OperationId, StorageName, lease.Kind, phase, lease.Key);

        foreach (var fault in _faultInjector.Evaluate(operationContext))
        {
            switch (fault)
            {
                case StorageDelayFault delay:
                    _context.TraceEvent(
                        $"storage:{StorageName}:operation:{lease.OperationId}:fault-delay:{phase}:{delay.Delay.Ticks}");
                    await Task.Delay(delay.Delay, _context.TimeProvider, token);
                    break;
                case StorageFailureFault failure:
                    ThrowFailure(lease, phase, failure.Code);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported storage fault '{fault.GetType().Name}'.");
            }
        }
    }

    private void ThrowFailure(StorageOperationLease lease, StorageOperationPhase phase, string code)
    {
        _context.TraceEvent($"storage:{StorageName}:operation:{lease.OperationId}:fault:{phase}:{code}");

        if (phase is StorageOperationPhase.AfterCommit or StorageOperationPhase.AfterWrite or StorageOperationPhase.AfterDelete)
        {
            throw new SimulationStorageAmbiguousCommitException(StorageName, lease.OperationId, code);
        }

        throw new SimulationStorageTransientException(StorageName, lease.OperationId, code);
    }

    private async Task WaitForNodeAsync(CancellationToken cancellationToken)
    {
        if (_node is not null)
        {
            await _node.WaitUntilRunningAsync(cancellationToken);
        }
    }

    private CancellationTokenSource CreateExecutionCancellationSource(CancellationToken cancellationToken)
    {
        return _node is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _context.CancellationToken)
            : _node.CreateExecutionCancellationSource(cancellationToken);
    }

    private TimeSpan GetBaseLatency(StorageOperationKind kind)
    {
        return kind == StorageOperationKind.Commit ? _options.CommitLatency : _options.OperationLatency;
    }

    private static StorageOperationPhase GetBeforePhase(StorageOperationKind kind)
    {
        return kind switch
        {
            StorageOperationKind.Read => StorageOperationPhase.BeforeRead,
            StorageOperationKind.Write => StorageOperationPhase.BeforeWrite,
            StorageOperationKind.Delete => StorageOperationPhase.BeforeDelete,
            StorageOperationKind.Commit => StorageOperationPhase.BeforeCommit,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown storage operation kind.")
        };
    }

    private static StorageOperationPhase GetAfterPhase(StorageOperationKind kind)
    {
        return kind switch
        {
            StorageOperationKind.Read => StorageOperationPhase.AfterRead,
            StorageOperationKind.Write => StorageOperationPhase.AfterWrite,
            StorageOperationKind.Delete => StorageOperationPhase.AfterDelete,
            StorageOperationKind.Commit => StorageOperationPhase.AfterCommit,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown storage operation kind.")
        };
    }

    private void EnsureLeaseOwnership(StorageOperationLease lease)
    {
        if (!lease.BelongsTo(this))
        {
            throw new ArgumentException("The storage operation lease belongs to a different boundary.", nameof(lease));
        }
    }

    private static string FormatKey(string? key)
    {
        return key ?? "-";
    }
}
