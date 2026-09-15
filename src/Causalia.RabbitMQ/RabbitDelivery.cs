namespace Causalia.RabbitMQ;

/// <summary>
/// Represents one RabbitMQ delivery that can be acknowledged or negatively acknowledged.
/// </summary>
public sealed class RabbitDelivery
{
    internal RabbitDelivery(long deliveryTag, string queue, string routingKey, ReadOnlyMemory<byte> body, bool redelivered)
    {
        DeliveryTag = deliveryTag;
        Queue = queue;
        RoutingKey = routingKey;
        Body = body;
        Redelivered = redelivered;
    }

    /// <summary>
    /// Gets the consumer-local delivery tag.
    /// </summary>
    public long DeliveryTag { get; }

    /// <summary>
    /// Gets the queue name.
    /// </summary>
    public string Queue { get; }

    /// <summary>
    /// Gets the routing key used by the publisher.
    /// </summary>
    public string RoutingKey { get; }

    /// <summary>
    /// Gets an immutable copy of the message body.
    /// </summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>
    /// Gets whether this message was previously delivered and returned to the queue.
    /// </summary>
    public bool Redelivered { get; }
}
