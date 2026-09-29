using Causalia.Faults;

namespace Causalia.AzureServiceBus;

/// <summary>Simulation-local broker for observable queue and topic subscription behavior.</summary>
public sealed class SimulationServiceBus
{
    private readonly SimulationContext _context;
    private readonly FaultInjector<ServiceBusSettlementEvent, ServiceBusSettlementFault>? _faults;
    private readonly Dictionary<string, Entity> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _topics = new(StringComparer.Ordinal);
    private long _nextSequence;

    internal SimulationServiceBus(
        SimulationContext context,
        FaultPlan<ServiceBusSettlementEvent, ServiceBusSettlementFault>? faultPlan)
    {
        _context = context;
        _faults = faultPlan is null ? null : context.CreateFaultInjector("azure-service-bus:settlement", faultPlan);
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public void CreateQueue(string name, TimeSpan? lockDuration = null, int maxDeliveryCount = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (lockDuration is { Ticks: <= 0 } || maxDeliveryCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(lockDuration));
        _entities.Add(name, new Entity(lockDuration ?? TimeSpan.FromMinutes(1), maxDeliveryCount));
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public void CreateTopic(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _topics.Add(name, new List<string>());
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public void CreateSubscription(string topic, string subscription, TimeSpan? lockDuration = null, int maxDeliveryCount = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        var subscriptions = _topics[topic];
        var entityName = $"{topic}/subscriptions/{subscription}";
        CreateQueue(entityName, lockDuration, maxDeliveryCount);
        subscriptions.Add(entityName);
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public async Task SendAsync(
        string destination,
        ReadOnlyMemory<byte> body,
        DateTimeOffset? scheduledFor,
        CancellationToken cancellationToken)
    {
        await SendAsync(destination, body, scheduledFor, null, cancellationToken);
    }

    /// <summary>Sends a message associated with one ordered session.</summary>
    public async Task SendAsync(
        string destination,
        ReadOnlyMemory<byte> body,
        DateTimeOffset? scheduledFor,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sessionId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var destinations = _topics.TryGetValue(destination, out var subscriptions)
            ? subscriptions : new List<string> { destination };
        foreach (var name in destinations)
        {
            var entity = GetEntity(name);
            var sequence = checked(++_nextSequence);
            entity.Messages.Add(new Message(sequence, body.ToArray(), scheduledFor ?? _context.TimeProvider.GetUtcNow(), sessionId));
            _context.TraceEvent($"servicebus:sent:{name}:{sequence}");
        }
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public async Task<ServiceBusDelivery?> ReceiveAsync(string entityName, CancellationToken cancellationToken)
    {
        return await ReceiveAsync(entityName, null, cancellationToken);
    }

    internal async Task<ServiceBusDelivery?> ReceiveAsync(
        string entityName, string? sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var entity = GetEntity(entityName);
        var now = _context.TimeProvider.GetUtcNow();
        foreach (var message in entity.Messages.ToArray())
        {
            if (!string.Equals(message.SessionId, sessionId, StringComparison.Ordinal))
                continue;
            if (message.LockedUntil is { } expiry && expiry <= now)
            {
                message.LockToken = null;
                message.LockedUntil = null;
                _context.TraceEvent($"servicebus:lock-expired:{entityName}:{message.Sequence}");
            }
            if (message.LockToken is not null || message.AvailableAt > now)
            {
                if (sessionId is not null)
                    return null;
                continue;
            }
            if (message.DeliveryCount >= entity.MaxDeliveryCount)
            {
                entity.DeadLetters.Add(message);
                entity.Messages.Remove(message);
                _context.TraceEvent($"servicebus:deadletter:max-deliveries:{entityName}:{message.Sequence}");
                continue;
            }
            message.DeliveryCount++;
            message.LockToken = CreateLockToken(message.Sequence, message.DeliveryCount);
            message.LockedUntil = now + entity.LockDuration;
            _context.TraceEvent($"servicebus:delivered:{entityName}:{message.Sequence}:{message.DeliveryCount}");
            return new ServiceBusDelivery(message.Sequence, message.Body, message.DeliveryCount,
                message.LockToken.Value, message.LockedUntil.Value);
        }
        return null;
    }

    /// <summary>Accepts exclusive ownership of one session until closed or its virtual lock expires.</summary>
    public async Task<SimulationServiceBusSession> AcceptSessionAsync(
        string entityName, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        var entity = GetEntity(entityName);
        var now = _context.TimeProvider.GetUtcNow();
        if (!entity.Messages.Any(message => message.SessionId == sessionId))
            throw new InvalidOperationException($"Session '{sessionId}' contains no messages.");
        if (entity.SessionOwners.TryGetValue(sessionId, out var current) && current.ExpiresAt > now)
            throw new SimulationServiceBusException("Session is already owned by another receiver.", false);
        if (current is not null)
            CloseSession(entityName, sessionId, current.Token);
        var token = checked(++entity.NextSessionOwner);
        entity.SessionOwners[sessionId] = new SessionOwner(token, now + entity.LockDuration);
        _context.TraceEvent($"servicebus:session-accepted:{entityName}:{sessionId}");
        return new SimulationServiceBusSession(this, entityName, sessionId, token);
    }

    internal void EnsureSession(string entityName, string sessionId, long ownerToken)
    {
        var entity = GetEntity(entityName);
        if (!entity.SessionOwners.TryGetValue(sessionId, out var owner) || owner.Token != ownerToken ||
            owner.ExpiresAt <= _context.TimeProvider.GetUtcNow())
            throw new SimulationServiceBusException("Session lock is no longer valid.", false);
    }

    internal void RenewSession(string entityName, string sessionId, long ownerToken)
    {
        EnsureSession(entityName, sessionId, ownerToken);
        var entity = GetEntity(entityName);
        entity.SessionOwners[sessionId] = new SessionOwner(ownerToken, _context.TimeProvider.GetUtcNow() + entity.LockDuration);
        _context.TraceEvent($"servicebus:session-renewed:{entityName}:{sessionId}");
    }

    internal void CloseSession(string entityName, string sessionId, long ownerToken)
    {
        var entity = GetEntity(entityName);
        if (!entity.SessionOwners.TryGetValue(sessionId, out var owner) || owner.Token != ownerToken)
            return;
        entity.SessionOwners.Remove(sessionId);
        foreach (var message in entity.Messages.Where(message => message.SessionId == sessionId))
        {
            message.LockToken = null;
            message.LockedUntil = null;
        }
        _context.TraceEvent($"servicebus:session-closed:{entityName}:{sessionId}");
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public Task CompleteAsync(string entityName, ServiceBusDelivery delivery, CancellationToken cancellationToken) =>
        SettleAsync(entityName, delivery, ServiceBusSettlementAction.Complete, cancellationToken);

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public Task AbandonAsync(string entityName, ServiceBusDelivery delivery, CancellationToken cancellationToken) =>
        SettleAsync(entityName, delivery, ServiceBusSettlementAction.Abandon, cancellationToken);

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public Task DeadLetterAsync(string entityName, ServiceBusDelivery delivery, CancellationToken cancellationToken) =>
        SettleAsync(entityName, delivery, ServiceBusSettlementAction.DeadLetter, cancellationToken);

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public async Task<ServiceBusDelivery> RenewLockAsync(
        string entityName, ServiceBusDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        var entity = GetEntity(entityName);
        var message = FindLocked(entity, delivery);
        message.LockedUntil = _context.TimeProvider.GetUtcNow() + entity.LockDuration;
        _context.TraceEvent($"servicebus:lock-renewed:{entityName}:{message.Sequence}");
        return delivery with { LockedUntil = message.LockedUntil.Value };
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public IReadOnlyList<ServiceBusDelivery> DeadLetters(string entityName) =>
        GetEntity(entityName).DeadLetters.Select(message =>
            new ServiceBusDelivery(message.Sequence, message.Body, message.DeliveryCount, Guid.Empty, default)).ToArray();

    private async Task SettleAsync(
        string entityName, ServiceBusDelivery delivery, ServiceBusSettlementAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var entity = GetEntity(entityName);
        var message = FindLocked(entity, delivery);
        var fault = _faults?.Evaluate(new ServiceBusSettlementEvent(entityName, message.Sequence, action))
            .FirstOrDefault() ?? ServiceBusSettlementFault.None;
        if (fault == ServiceBusSettlementFault.Reject)
            throw new SimulationServiceBusException("Settlement was rejected by the broker.", false);
        switch (action)
        {
            case ServiceBusSettlementAction.Complete:
                entity.Messages.Remove(message);
                break;
            case ServiceBusSettlementAction.Abandon:
                message.LockToken = null;
                message.LockedUntil = null;
                break;
            case ServiceBusSettlementAction.DeadLetter:
                entity.Messages.Remove(message);
                entity.DeadLetters.Add(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
        _context.TraceEvent($"servicebus:settled:{action}:{entityName}:{message.Sequence}");
        if (fault == ServiceBusSettlementFault.LoseAcknowledgement)
        {
            _context.TraceEvent($"servicebus:settlement-ack-lost:{entityName}:{message.Sequence}");
            throw new SimulationServiceBusException("Settlement succeeded but the client did not observe its acknowledgement.", true);
        }
    }

    private Message FindLocked(Entity entity, ServiceBusDelivery delivery)
    {
        var message = entity.Messages.FirstOrDefault(value => value.Sequence == delivery.SequenceNumber);
        if (message is null || message.LockToken != delivery.LockToken ||
            message.LockedUntil <= _context.TimeProvider.GetUtcNow())
            throw new SimulationServiceBusException("The delivery lock is no longer valid.", false);
        return message;
    }

    private Entity GetEntity(string name) => _entities.TryGetValue(name, out var entity)
        ? entity : throw new ArgumentException($"Unknown Service Bus entity '{name}'.", nameof(name));

    private static Guid CreateLockToken(long sequence, int count)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, sequence);
        BitConverter.TryWriteBytes(bytes[8..], count);
        return new Guid(bytes);
    }

    private sealed class Entity(TimeSpan lockDuration, int maxDeliveryCount)
    {
        public TimeSpan LockDuration { get; } = lockDuration;
        public int MaxDeliveryCount { get; } = maxDeliveryCount;
        public List<Message> Messages { get; } = new();
        public List<Message> DeadLetters { get; } = new();
        public Dictionary<string, SessionOwner> SessionOwners { get; } = new(StringComparer.Ordinal);
        public long NextSessionOwner { get; set; }
    }

    private sealed record SessionOwner(long Token, DateTimeOffset ExpiresAt);

    private sealed class Message(long sequence, byte[] body, DateTimeOffset availableAt, string? sessionId)
    {
        public long Sequence { get; } = sequence;
        public byte[] Body { get; } = body;
        public DateTimeOffset AvailableAt { get; } = availableAt;
        public string? SessionId { get; } = sessionId;
        public int DeliveryCount { get; set; }
        public Guid? LockToken { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
