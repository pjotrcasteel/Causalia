namespace Causalia.Kafka;

/// <summary>
/// Describes one Kafka offset commit for deterministic fault evaluation.
/// </summary>
public sealed record KafkaCommitFaultContext
{
    /// <summary>Gets the consumer group id.</summary>
    public required string GroupId { get; init; }
    /// <summary>Gets the member id.</summary>
    public required string MemberId { get; init; }
    /// <summary>Gets the partition.</summary>
    public required KafkaTopicPartition Partition { get; init; }
    /// <summary>Gets the next committed offset.</summary>
    public required long Offset { get; init; }
}
