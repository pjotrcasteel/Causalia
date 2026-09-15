namespace Causalia.RabbitMQ;

/// <summary>
/// Simulates one RabbitMQ consumer with manual acknowledgements and prefetch flow control.
/// </summary>
public sealed class SimulationRabbitConsumer : IAsyncDisposable
{
    private readonly SimulationRabbitBroker _broker;
    private readonly Dictionary<long, RabbitQueuedMessage> _unacked = [];
    private bool _disposed;
    private long _nextDeliveryTag;

    internal SimulationRabbitConsumer(SimulationRabbitBroker broker, string consumerName, string queue, ushort prefetchCount)
    {
        _broker = broker;
        ConsumerName = consumerName;
        Queue = queue;
        PrefetchCount = prefetchCount;
    }

    /// <summary>
    /// Gets the logical consumer name.
    /// </summary>
    public string ConsumerName { get; }

    /// <summary>
    /// Gets the queue from which this consumer receives messages.
    /// </summary>
    public string Queue { get; }

    /// <summary>
    /// Gets the maximum number of unacknowledged deliveries, or zero for unlimited.
    /// </summary>
    public ushort PrefetchCount { get; }

    /// <summary>
    /// Gets the current number of unacknowledged deliveries.
    /// </summary>
    public int UnacknowledgedCount => _unacked.Count;

    /// <summary>
    /// Receives one message when prefetch allows it, or null when no delivery is currently available.
    /// </summary>
    public async Task<RabbitDelivery?> ReceiveAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();

        if (PrefetchCount > 0 && _unacked.Count >= PrefetchCount)
        {
            return null;
        }

        var message = _broker.Dequeue(Queue);

        if (message is null)
        {
            return null;
        }

        var deliveryTag = checked(++_nextDeliveryTag);
        _unacked.Add(deliveryTag, message);
        _broker.TraceDelivery(ConsumerName, Queue, deliveryTag, message);
        return new RabbitDelivery(deliveryTag, Queue, message.RoutingKey, message.Body.ToArray(), message.Redelivered);
    }

    /// <summary>
    /// Acknowledges one delivery and removes it from the unacknowledged window.
    /// </summary>
    public Task AckAsync(long deliveryTag, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var message = TakeUnacked(deliveryTag);
        _broker.TraceAck(ConsumerName, Queue, deliveryTag, message.MessageId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Negatively acknowledges one delivery and optionally requeues it for redelivery.
    /// </summary>
    public Task NackAsync(long deliveryTag, bool requeue, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var message = TakeUnacked(deliveryTag);

        if (requeue)
        {
            _broker.Requeue(Queue, message);
        }

        _broker.TraceNack(ConsumerName, Queue, deliveryTag, message.MessageId, requeue);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Closes the consumer and requeues every still-unacknowledged delivery as redelivered.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;

        foreach (var message in _unacked.OrderByDescending(pair => pair.Key).Select(pair => pair.Value))
        {
            _broker.Requeue(Queue, message);
        }

        _unacked.Clear();
        _broker.TraceConsumerClosed(ConsumerName, Queue);
        return ValueTask.CompletedTask;
    }

    private RabbitQueuedMessage TakeUnacked(long deliveryTag)
    {
        if (!_unacked.Remove(deliveryTag, out var message))
        {
            throw new InvalidOperationException($"Delivery tag '{deliveryTag}' is not outstanding for consumer '{ConsumerName}'.");
        }

        return message;
    }
}
