namespace Causalia.Dapr;

/// <summary>
/// Configures deterministic Dapr pub/sub redelivery behavior.
/// </summary>
public sealed class DaprSubscriptionOptions
{
    /// <summary>
    /// Gets or initializes the maximum number of delivery attempts including the first attempt.
    /// </summary>
    public int MaxDeliveryAttempts { get; init; } = 3;

    /// <summary>
    /// Gets or initializes the virtual delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or initializes an optional dead-letter topic that receives exhausted deliveries.
    /// </summary>
    public string? DeadLetterTopic { get; init; }
}
