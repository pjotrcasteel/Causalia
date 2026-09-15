namespace Causalia.RabbitMQ;

/// <summary>
/// Simulates RabbitMQ direct/fanout routing, queues, prefetch and acknowledgement-driven redelivery.
/// </summary>
public sealed class SimulationRabbitBroker
{
    private readonly SimulationContext _context;
    private readonly Causalia.Faults.FaultInjector<RabbitPublishFaultContext, RabbitPublishFault>? _publishFaults;
    private readonly Dictionary<string, RabbitExchangeState> _exchanges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RabbitQueueState> _queues = new(StringComparer.Ordinal);
    private long _nextMessageId;

    internal SimulationRabbitBroker(SimulationContext context, string name, RabbitFaultPlan? faults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _context = context;
        Name = name;
        _publishFaults = faults is null
            ? null
            : context.CreateFaultInjector($"rabbitmq:{name}:publish", faults.Plan);
        _context.TraceEvent($"rabbitmq:broker:created:{Name}");
    }

    /// <summary>
    /// Gets the logical broker name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Declares an exchange.
    /// </summary>
    public void DeclareExchange(string exchange, RabbitExchangeType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        if (!_exchanges.TryAdd(exchange, new RabbitExchangeState { Type = type }))
        {
            throw new InvalidOperationException($"RabbitMQ exchange '{exchange}' already exists.");
        }

        _context.TraceEvent($"rabbitmq:exchange:declared:{Name}:{exchange}:{type}");
    }

    /// <summary>
    /// Declares a durable logical queue whose messages survive consumer lifecycles.
    /// </summary>
    public void DeclareQueue(string queue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queue);

        if (!_queues.TryAdd(queue, new RabbitQueueState()))
        {
            throw new InvalidOperationException($"RabbitMQ queue '{queue}' already exists.");
        }

        _context.TraceEvent($"rabbitmq:queue:declared:{Name}:{queue}");
    }

    /// <summary>
    /// Binds a queue to an exchange using a routing key.
    /// </summary>
    public void BindQueue(string exchange, string queue, string routingKey = "")
    {
        var exchangeState = GetExchange(exchange);
        _ = GetQueue(queue);
        exchangeState.Bindings.Add(new RabbitBinding(queue, routingKey));
        _context.TraceEvent($"rabbitmq:queue:bound:{Name}:{exchange}:{queue}:{routingKey}");
    }

    /// <summary>
    /// Publishes one body and deterministically routes copies to all matching queues.
    /// </summary>
    public async Task<RabbitPublishResult> PublishAsync(
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        var exchangeState = GetExchange(exchange);
        var messageId = checked(++_nextMessageId);
        var faultContext = new RabbitPublishFaultContext
        {
            Exchange = exchange,
            RoutingKey = routingKey,
            MessageId = messageId
        };
        var confirmLost = false;

        foreach (var fault in _publishFaults?.Evaluate(faultContext) ?? [])
        {
            switch (fault)
            {
                case RabbitPublishDelayFault delayed:
                    _context.TraceEvent($"rabbitmq:publish:delayed:{Name}:{messageId}:{delayed.Delay.Ticks}");
                    await Task.Delay(delayed.Delay, _context.TimeProvider, cancellationToken);
                    break;
                case RabbitPublishRejectedFault:
                    _context.TraceEvent($"rabbitmq:publish:rejected:{Name}:{exchange}:{messageId}");
                    throw new SimulationRabbitPublishException("RabbitMQ publish was rejected before broker acceptance.", false);
                case RabbitPublisherConfirmLostFault:
                    confirmLost = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported RabbitMQ publish fault '{fault.GetType().FullName}'.");
            }
        }

        var matching = exchangeState.Bindings.Where(binding => Matches(exchangeState.Type, binding, routingKey))
            .Select(binding => binding.Queue)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var queue in matching)
        {
            GetQueue(queue).Ready.AddLast(new RabbitQueuedMessage(messageId, routingKey, body.ToArray(), false));
        }

        _context.TraceEvent($"rabbitmq:published:{Name}:{exchange}:{routingKey}:{messageId}:queues:{matching.Count}");

        if (confirmLost)
        {
            _context.TraceEvent($"rabbitmq:publish:confirm-lost:{Name}:{exchange}:{messageId}");
            throw new SimulationRabbitPublishException("RabbitMQ publish was routed but its publisher confirm was lost.", true);
        }

        return new RabbitPublishResult { RoutedQueueCount = matching.Count };
    }

    /// <summary>
    /// Creates a manual-acknowledgement consumer for a queue.
    /// </summary>
    public SimulationRabbitConsumer CreateConsumer(string queue, string consumerName, ushort prefetchCount = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        _ = GetQueue(queue);
        _context.TraceEvent($"rabbitmq:consumer:created:{Name}:{queue}:{consumerName}:prefetch:{prefetchCount}");
        return new SimulationRabbitConsumer(this, consumerName, queue, prefetchCount);
    }

    internal RabbitQueuedMessage? Dequeue(string queue)
    {
        var ready = GetQueue(queue).Ready;

        if (ready.First is null)
        {
            return null;
        }

        var message = ready.First.Value;
        ready.RemoveFirst();
        return message;
    }

    internal void Requeue(string queue, RabbitQueuedMessage message)
    {
        GetQueue(queue).Ready.AddFirst(message with { Redelivered = true });
        _context.TraceEvent($"rabbitmq:requeued:{Name}:{queue}:{message.MessageId}");
    }

    internal void TraceDelivery(string consumer, string queue, long deliveryTag, RabbitQueuedMessage message)
    {
        _context.TraceEvent(
            $"rabbitmq:delivered:{Name}:{queue}:{consumer}:{message.MessageId}:tag:{deliveryTag}:redelivered:{message.Redelivered}");
    }

    internal void TraceAck(string consumer, string queue, long deliveryTag, long messageId)
    {
        _context.TraceEvent($"rabbitmq:acked:{Name}:{queue}:{consumer}:{messageId}:tag:{deliveryTag}");
    }

    internal void TraceNack(string consumer, string queue, long deliveryTag, long messageId, bool requeue)
    {
        _context.TraceEvent($"rabbitmq:nacked:{Name}:{queue}:{consumer}:{messageId}:tag:{deliveryTag}:requeue:{requeue}");
    }

    internal void TraceConsumerClosed(string consumer, string queue)
    {
        _context.TraceEvent($"rabbitmq:consumer:closed:{Name}:{queue}:{consumer}");
    }

    private static bool Matches(RabbitExchangeType type, RabbitBinding binding, string routingKey)
    {
        return type == RabbitExchangeType.Fanout || string.Equals(binding.RoutingKey, routingKey, StringComparison.Ordinal);
    }

    private RabbitExchangeState GetExchange(string exchange)
    {
        return _exchanges.TryGetValue(exchange, out var state)
            ? state
            : throw new InvalidOperationException($"RabbitMQ exchange '{exchange}' does not exist.");
    }

    private RabbitQueueState GetQueue(string queue)
    {
        return _queues.TryGetValue(queue, out var state)
            ? state
            : throw new InvalidOperationException($"RabbitMQ queue '{queue}' does not exist.");
    }
}
