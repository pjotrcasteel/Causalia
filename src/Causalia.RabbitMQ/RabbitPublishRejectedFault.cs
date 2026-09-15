namespace Causalia.RabbitMQ;

/// <summary>
/// Rejects a publish before any queue receives it.
/// </summary>
public sealed record RabbitPublishRejectedFault : RabbitPublishFault;
