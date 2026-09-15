namespace Causalia.Kafka;

/// <summary>
/// Describes the durable log position assigned to a produced Kafka record.
/// </summary>
public sealed record KafkaProduceResult
{
    /// <summary>
    /// Gets the topic name.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the assigned partition.
    /// </summary>
    public required int Partition { get; init; }

    /// <summary>
    /// Gets the assigned offset.
    /// </summary>
    public required long Offset { get; init; }
}
