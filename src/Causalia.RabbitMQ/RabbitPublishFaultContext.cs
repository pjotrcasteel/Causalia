namespace Causalia.RabbitMQ;

/// <summary>
/// Describes one RabbitMQ publish for deterministic fault evaluation.
/// </summary>
public sealed record RabbitPublishFaultContext
{
    /// <summary>Gets the exchange.</summary>
    public required string Exchange { get; init; }
    /// <summary>Gets the routing key.</summary>
    public required string RoutingKey { get; init; }
    /// <summary>Gets the logical message id.</summary>
    public required long MessageId { get; init; }
}
