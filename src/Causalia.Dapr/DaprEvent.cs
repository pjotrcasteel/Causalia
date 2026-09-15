namespace Causalia.Dapr;

/// <summary>
/// Represents one deterministic Dapr pub/sub delivery.
/// </summary>
public sealed class DaprEvent<T>
{
    internal DaprEvent(long messageId, string pubSubName, string topic, T data, int attempt)
    {
        MessageId = messageId;
        PubSubName = pubSubName;
        Topic = topic;
        Data = data;
        Attempt = attempt;
    }

    /// <summary>
    /// Gets the stable logical message identifier shared by all redelivery attempts.
    /// </summary>
    public long MessageId { get; }

    /// <summary>
    /// Gets the logical pub/sub component name.
    /// </summary>
    public string PubSubName { get; }

    /// <summary>
    /// Gets the topic name.
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Gets the event payload.
    /// </summary>
    public T Data { get; }

    /// <summary>
    /// Gets the one-based delivery attempt.
    /// </summary>
    public int Attempt { get; }
}
