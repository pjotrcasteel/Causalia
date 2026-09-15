namespace Causalia.RabbitMQ;

internal sealed record RabbitQueuedMessage(long MessageId, string RoutingKey, ReadOnlyMemory<byte> Body, bool Redelivered);
