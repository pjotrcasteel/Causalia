namespace Causalia.Kafka;

/// <summary>
/// Represents one consumed Kafka record.
/// </summary>
public sealed class KafkaRecord<T>
{
    internal KafkaRecord(string topic, int partition, long offset, string? key, T value)
    {
        Topic = topic;
        Partition = partition;
        Offset = offset;
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Gets the topic name.
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Gets the zero-based partition number.
    /// </summary>
    public int Partition { get; }

    /// <summary>
    /// Gets the zero-based record offset.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the optional record key.
    /// </summary>
    public string? Key { get; }

    /// <summary>
    /// Gets the deserialized record value.
    /// </summary>
    public T Value { get; }
}
