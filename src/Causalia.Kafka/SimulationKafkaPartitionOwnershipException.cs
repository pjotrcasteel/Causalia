namespace Causalia.Kafka;

/// <summary>
/// Thrown when a consumer tries to commit an offset for a partition it no longer owns.
/// </summary>
public sealed class SimulationKafkaPartitionOwnershipException : InvalidOperationException
{
    /// <summary>
    /// Initializes a partition-ownership exception.
    /// </summary>
    public SimulationKafkaPartitionOwnershipException(string memberId, KafkaTopicPartition partition)
        : base($"Kafka consumer '{memberId}' no longer owns partition '{partition.Topic}[{partition.Partition}]'.")
    {
    }
}
