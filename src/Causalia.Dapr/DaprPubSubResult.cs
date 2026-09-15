namespace Causalia.Dapr;

/// <summary>
/// Represents the application acknowledgement returned to Dapr pub/sub delivery.
/// </summary>
public enum DaprPubSubResult
{
    /// <summary>
    /// The event was processed successfully and must not be redelivered.
    /// </summary>
    Success,

    /// <summary>
    /// The event should be redelivered according to the subscription retry policy.
    /// </summary>
    Retry,

    /// <summary>
    /// The event should be dropped without further delivery attempts.
    /// </summary>
    Drop
}
