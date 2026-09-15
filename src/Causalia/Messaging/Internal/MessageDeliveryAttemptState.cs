namespace Causalia.Messaging.Internal;

internal sealed class MessageDeliveryAttemptState
{
    public required int NextAttempt { get; set; }

    public required int RemainingDeliveries { get; set; }
}
