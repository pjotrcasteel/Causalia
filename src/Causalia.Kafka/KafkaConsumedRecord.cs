namespace Causalia.Kafka;

internal sealed record KafkaConsumedRecord(KafkaTopicPartition Partition, KafkaStoredRecord Value);
