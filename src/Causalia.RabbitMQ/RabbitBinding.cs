namespace Causalia.RabbitMQ;

internal sealed record RabbitBinding(string Queue, string RoutingKey);
