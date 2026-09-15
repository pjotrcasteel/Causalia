namespace Causalia.Messaging;

/// <summary>
/// Describes one deterministic message delivery.
/// </summary>
public sealed class MessageDeliveryContext
{
    internal MessageDeliveryContext(long messageId, string endpoint, int attempt, DateTimeOffset enqueuedAt, DateTimeOffset deliveredAt)
    {
        MessageId = messageId;
        Endpoint = endpoint;
        Attempt = attempt;
        EnqueuedAt = enqueuedAt;
        DeliveredAt = deliveredAt;
    }

    /// <summary>
    /// Gets the deterministic identifier assigned to the message by this bus.
    /// </summary>
    public long MessageId { get; }

    /// <summary>
    /// Gets the logical destination endpoint.
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// Gets the delivery attempt number.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Gets the virtual time at which the message was enqueued.
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; }

    /// <summary>
    /// Gets the virtual time at which handler execution started.
    /// </summary>
    public DateTimeOffset DeliveredAt { get; }
}
