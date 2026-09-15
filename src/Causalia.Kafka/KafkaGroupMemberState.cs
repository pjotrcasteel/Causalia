namespace Causalia.Kafka;

internal sealed class KafkaGroupMemberState
{
    public required string MemberId { get; init; }

    public required KafkaConsumerOptions Options { get; init; }

    public HashSet<string> Topics { get; } = new(StringComparer.Ordinal);

    public HashSet<KafkaTopicPartition> Assignment { get; } = [];

    public Dictionary<KafkaTopicPartition, long> Positions { get; } = [];

    public Dictionary<KafkaTopicPartition, long> StoredOffsets { get; } = [];
}
