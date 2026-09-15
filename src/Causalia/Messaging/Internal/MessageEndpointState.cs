namespace Causalia.Messaging.Internal;

internal sealed class MessageEndpointState
{
    public LinkedList<QueuedMessage> Pending { get; } = new();

    public bool IsProcessing { get; set; }
}
