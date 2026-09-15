namespace Causalia.Dapr;

/// <summary>
/// Simulates one Dapr pub/sub component with deterministic acknowledgement and redelivery behavior.
/// </summary>
public sealed class SimulationDaprPubSub
{
    private readonly Dictionary<string, List<InternalDaprSubscription>> _subscriptions = new(StringComparer.Ordinal);
    private readonly SimulationContext _context;
    private readonly Causalia.Faults.FaultInjector<DaprDeliveryFaultContext, DaprDeliveryFault>? _faults;
    private long _nextMessageId;
    private long _nextSubscriptionId;

    internal SimulationDaprPubSub(SimulationContext context, string name, DaprPubSubOptions options)
    {
        _context = context;
        Name = name;
        _faults = options.Faults is null
            ? null
            : context.CreateFaultInjector($"dapr:{name}:pubsub", options.Faults.Plan);
        _context.TraceEvent($"dapr:pubsub:created:{Name}");
    }

    /// <summary>
    /// Gets the logical Dapr pub/sub component name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Registers one typed topic subscription.
    /// </summary>
    public DaprSubscription Subscribe<T>(
        string topic,
        string subscriberName,
        Func<DaprEvent<T>, CancellationToken, Task<DaprPubSubResult>> handler,
        DaprSubscriptionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriberName);
        ArgumentNullException.ThrowIfNull(handler);
        var configured = options ?? new DaprSubscriptionOptions();
        Validate(configured);
        var id = checked(++_nextSubscriptionId);
        var subscription = new InternalDaprSubscription
        {
            Id = id,
            SubscriberName = subscriberName,
            PayloadType = typeof(T),
            Options = configured,
            Handler = async (messageId, value, attempt, cancellationToken) =>
                await handler(new DaprEvent<T>(messageId, Name, topic, (T)value, attempt), cancellationToken)
        };

        if (!_subscriptions.TryGetValue(topic, out var topicSubscriptions))
        {
            topicSubscriptions = [];
            _subscriptions.Add(topic, topicSubscriptions);
        }

        topicSubscriptions.Add(subscription);
        _context.TraceEvent($"dapr:pubsub:subscribed:{Name}:{topic}:{subscriberName}:{id}");
        return new DaprSubscription(() => Remove(topic, id));
    }

    /// <summary>
    /// Publishes one typed event and completes after all current subscribers have acknowledged or exhausted it.
    /// </summary>
    public async Task PublishAsync<T>(string topic, T data, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        cancellationToken.ThrowIfCancellationRequested();
        var messageId = checked(++_nextMessageId);
        _context.TraceEvent($"dapr:pubsub:published:{Name}:{topic}:{messageId}");

        if (!_subscriptions.TryGetValue(topic, out var subscriptions))
        {
            return;
        }

        foreach (var subscription in subscriptions.ToList())
        {
            if (subscription.PayloadType != typeof(T))
            {
                throw new InvalidOperationException(
                    $"Dapr topic '{topic}' expects payload '{subscription.PayloadType.FullName}', not '{typeof(T).FullName}'.");
            }

            await DeliverAsync(topic, data!, messageId, subscription, cancellationToken);
        }
    }

    private async Task DeliverAsync(
        string topic,
        object data,
        long messageId,
        InternalDaprSubscription subscription,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= subscription.Options.MaxDeliveryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var faultContext = new DaprDeliveryFaultContext
            {
                MessageId = messageId,
                Topic = topic,
                SubscriberName = subscription.SubscriberName,
                Attempt = attempt
            };
            var faults = _faults?.Evaluate(faultContext) ?? [];
            var acknowledgementLost = false;

            foreach (var fault in faults)
            {
                switch (fault)
                {
                    case DaprDeliveryDelayFault delayed:
                        _context.TraceEvent($"dapr:pubsub:delayed:{Name}:{topic}:{messageId}:{delayed.Delay.Ticks}");
                        await Task.Delay(delayed.Delay, _context.TimeProvider, cancellationToken);
                        break;
                    case DaprAcknowledgementLostFault:
                        acknowledgementLost = true;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported Dapr delivery fault '{fault.GetType().FullName}'.");
                }
            }

            _context.TraceEvent(
                $"dapr:pubsub:delivered:{Name}:{topic}:{messageId}:{subscription.SubscriberName}:attempt:{attempt}");
            DaprPubSubResult result;

            try
            {
                result = await subscription.Handler(messageId, data, attempt, cancellationToken);

                if (result == DaprPubSubResult.Success && acknowledgementLost)
                {
                    _context.TraceEvent($"dapr:pubsub:ack-lost:{Name}:{topic}:{messageId}:attempt:{attempt}");
                    result = DaprPubSubResult.Retry;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                result = DaprPubSubResult.Retry;
            }

            if (result == DaprPubSubResult.Success)
            {
                _context.TraceEvent($"dapr:pubsub:completed:{Name}:{topic}:{messageId}:{subscription.SubscriberName}");
                return;
            }

            if (result == DaprPubSubResult.Drop)
            {
                _context.TraceEvent($"dapr:pubsub:dropped:{Name}:{topic}:{messageId}:{subscription.SubscriberName}");
                return;
            }

            if (attempt < subscription.Options.MaxDeliveryAttempts && subscription.Options.RetryDelay > TimeSpan.Zero)
            {
                _context.TraceEvent($"dapr:pubsub:retry:{Name}:{topic}:{messageId}:attempt:{attempt + 1}");
                await Task.Delay(subscription.Options.RetryDelay, _context.TimeProvider, cancellationToken);
            }
        }

        _context.TraceEvent($"dapr:pubsub:exhausted:{Name}:{topic}:{messageId}:{subscription.SubscriberName}");

        if (!string.IsNullOrWhiteSpace(subscription.Options.DeadLetterTopic))
        {
            await PublishDeadLetterAsync(subscription.Options.DeadLetterTopic, data, cancellationToken);
        }
    }

    private async Task PublishDeadLetterAsync(string topic, object data, CancellationToken cancellationToken)
    {
        var messageId = checked(++_nextMessageId);
        _context.TraceEvent($"dapr:pubsub:deadletter:{Name}:{topic}:{messageId}");

        if (!_subscriptions.TryGetValue(topic, out var subscriptions))
        {
            return;
        }

        foreach (var subscription in subscriptions.ToList())
        {
            if (!subscription.PayloadType.IsInstanceOfType(data))
            {
                continue;
            }

            _ = await subscription.Handler(messageId, data, 1, cancellationToken);
        }
    }

    private void Remove(string topic, long id)
    {
        if (!_subscriptions.TryGetValue(topic, out var subscriptions))
        {
            return;
        }

        subscriptions.RemoveAll(subscription => subscription.Id == id);
        _context.TraceEvent($"dapr:pubsub:unsubscribed:{Name}:{topic}:{id}");
    }

    private static void Validate(DaprSubscriptionOptions options)
    {
        if (options.MaxDeliveryAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxDeliveryAttempts must be greater than zero.");
        }

        if (options.RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RetryDelay cannot be negative.");
        }
    }
}
