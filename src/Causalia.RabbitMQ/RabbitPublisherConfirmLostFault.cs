namespace Causalia.RabbitMQ;

/// <summary>
/// Routes a publish but loses the publisher confirmation.
/// </summary>
public sealed record RabbitPublisherConfirmLostFault : RabbitPublishFault;
