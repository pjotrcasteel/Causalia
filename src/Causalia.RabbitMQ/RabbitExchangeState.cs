namespace Causalia.RabbitMQ;

internal sealed class RabbitExchangeState
{
    public required RabbitExchangeType Type { get; init; }

    public List<RabbitBinding> Bindings { get; } = [];
}
