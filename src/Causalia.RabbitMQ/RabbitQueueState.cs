namespace Causalia.RabbitMQ;

internal sealed class RabbitQueueState
{
    public LinkedList<RabbitQueuedMessage> Ready { get; } = [];
}
