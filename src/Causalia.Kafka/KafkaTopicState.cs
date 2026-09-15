namespace Causalia.Kafka;

internal sealed class KafkaTopicState
{
    public required string Name { get; init; }

    public required IReadOnlyList<List<KafkaStoredRecord>> Partitions { get; init; }

    public int NextUnkeyedPartition { get; set; }
}
