namespace Causalia.Kafka;

internal sealed class KafkaConsumerGroupState
{
    public required string GroupId { get; init; }

    public Dictionary<string, KafkaGroupMemberState> Members { get; } = new(StringComparer.Ordinal);

    public Dictionary<KafkaTopicPartition, long> CommittedOffsets { get; } = [];
}
