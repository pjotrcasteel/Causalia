namespace Causalia.Kafka;

internal sealed record KafkaStoredRecord(long Offset, string? Key, ReadOnlyMemory<byte> Value);
