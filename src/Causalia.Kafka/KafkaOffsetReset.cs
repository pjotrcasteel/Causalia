namespace Causalia.Kafka;

/// <summary>
/// Controls the initial consumer position when no committed offset exists.
/// </summary>
public enum KafkaOffsetReset
{
    /// <summary>
    /// Start at the oldest available record.
    /// </summary>
    Earliest,

    /// <summary>
    /// Start after the newest currently available record.
    /// </summary>
    Latest
}
