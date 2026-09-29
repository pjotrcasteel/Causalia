namespace Causalia.AzureServiceBus;

/// <summary>
/// Owns a session across deliveries; dispose it when a process stops to release unsettled locks.
/// </summary>
public sealed class SimulationServiceBusSession : IAsyncDisposable
{
    private readonly SimulationServiceBus _broker;
    private readonly string _entity;
    private readonly long _ownerToken;
    private bool _closed;

    internal SimulationServiceBusSession(SimulationServiceBus broker, string entity, string sessionId, long ownerToken)
    {
        _broker = broker;
        _entity = entity;
        SessionId = sessionId;
        _ownerToken = ownerToken;
    }

    /// <summary>Gets the session identifier.</summary>
    public string SessionId { get; }

    /// <summary>Receives the next available message in this session.</summary>
    public Task<ServiceBusDelivery?> ReceiveAsync(CancellationToken cancellationToken)
    {
        EnsureOpen();
        _broker.EnsureSession(_entity, SessionId, _ownerToken);
        return _broker.ReceiveAsync(_entity, SessionId, cancellationToken);
    }

    /// <summary>Completes a delivery owned by this session.</summary>
    public Task CompleteAsync(ServiceBusDelivery delivery, CancellationToken cancellationToken)
    {
        EnsureOpen();
        _broker.EnsureSession(_entity, SessionId, _ownerToken);
        return _broker.CompleteAsync(_entity, delivery, cancellationToken);
    }

    /// <summary>Abandons a delivery owned by this session.</summary>
    public Task AbandonAsync(ServiceBusDelivery delivery, CancellationToken cancellationToken)
    {
        EnsureOpen();
        _broker.EnsureSession(_entity, SessionId, _ownerToken);
        return _broker.AbandonAsync(_entity, delivery, cancellationToken);
    }

    /// <summary>Dead-letters a delivery owned by this session.</summary>
    public Task DeadLetterAsync(ServiceBusDelivery delivery, CancellationToken cancellationToken)
    {
        EnsureOpen();
        _broker.EnsureSession(_entity, SessionId, _ownerToken);
        return _broker.DeadLetterAsync(_entity, delivery, cancellationToken);
    }

    /// <summary>Renews the exclusive session lock using virtual time.</summary>
    public void RenewLock()
    {
        EnsureOpen();
        _broker.RenewSession(_entity, SessionId, _ownerToken);
    }

    /// <summary>Releases ownership and outstanding delivery locks after a process stops.</summary>
    public ValueTask DisposeAsync()
    {
        if (!_closed)
        {
            _closed = true;
            _broker.CloseSession(_entity, SessionId, _ownerToken);
        }

        return ValueTask.CompletedTask;
    }

    private void EnsureOpen()
    {
        ObjectDisposedException.ThrowIf(_closed, this);
    }
}
