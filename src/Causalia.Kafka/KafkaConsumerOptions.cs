namespace Causalia.Kafka;

/// <summary>
/// Configures deterministic Kafka consumer behavior.
/// </summary>
public sealed class KafkaConsumerOptions
{
    /// <summary>
    /// Gets or initializes the offset reset policy used when no committed offset exists.
    /// </summary>
    public KafkaOffsetReset AutoOffsetReset { get; init; } = KafkaOffsetReset.Earliest;
}
