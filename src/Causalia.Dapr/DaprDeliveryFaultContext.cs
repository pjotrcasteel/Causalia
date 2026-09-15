namespace Causalia.Dapr;

/// <summary>
/// Describes one Dapr delivery attempt for deterministic fault evaluation.
/// </summary>
public sealed record DaprDeliveryFaultContext
{
    /// <summary>Gets the stable logical message identifier.</summary>
    public required long MessageId { get; init; }
    /// <summary>Gets the topic name.</summary>
    public required string Topic { get; init; }
    /// <summary>Gets the subscriber name.</summary>
    public required string SubscriberName { get; init; }
    /// <summary>Gets the one-based delivery attempt.</summary>
    public required int Attempt { get; init; }
}
