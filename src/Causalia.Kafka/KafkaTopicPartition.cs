namespace Causalia.Kafka;

/// <summary>
/// Identifies one Kafka topic partition.
/// </summary>
public readonly record struct KafkaTopicPartition(string Topic, int Partition);
