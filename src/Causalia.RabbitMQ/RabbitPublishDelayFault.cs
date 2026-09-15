namespace Causalia.RabbitMQ;

/// <summary>
/// Adds virtual latency to one RabbitMQ publish.
/// </summary>
public sealed record RabbitPublishDelayFault(TimeSpan Delay) : RabbitPublishFault;
