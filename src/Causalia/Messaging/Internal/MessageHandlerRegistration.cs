using Causalia.Nodes;

namespace Causalia.Messaging.Internal;

internal sealed class MessageHandlerRegistration
{
    public required Func<object, MessageDeliveryContext, CancellationToken, Task> Handler { get; init; }

    public SimulationNode? Node { get; init; }
}
