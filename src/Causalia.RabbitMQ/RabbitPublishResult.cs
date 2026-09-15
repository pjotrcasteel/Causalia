namespace Causalia.RabbitMQ;

/// <summary>
/// Describes routing performed by one simulated RabbitMQ publish.
/// </summary>
public sealed record RabbitPublishResult
{
    /// <summary>
    /// Gets the number of queues to which the message was routed.
    /// </summary>
    public required int RoutedQueueCount { get; init; }

    /// <summary>
    /// Gets whether at least one queue received the publish.
    /// </summary>
    public bool WasRouted => RoutedQueueCount > 0;
}
