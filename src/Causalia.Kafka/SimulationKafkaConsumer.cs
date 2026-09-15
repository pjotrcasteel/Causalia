using System.Text.Json;

namespace Causalia.Kafka;

/// <summary>
/// Represents one deterministic member of a Kafka consumer group.
/// </summary>
public sealed class SimulationKafkaConsumer : IAsyncDisposable
{
    private readonly SimulationKafkaCluster _cluster;
    private bool _disposed;

    internal SimulationKafkaConsumer(SimulationKafkaCluster cluster, string groupId, string memberId, KafkaConsumerOptions options)
    {
        _cluster = cluster;
        GroupId = groupId;
        MemberId = memberId;
        Options = options;
    }

    /// <summary>
    /// Gets the consumer group identifier.
    /// </summary>
    public string GroupId { get; }

    /// <summary>
    /// Gets the stable member identifier for this consumer generation.
    /// </summary>
    public string MemberId { get; }

    /// <summary>
    /// Gets the configured consumer behavior.
    /// </summary>
    public KafkaConsumerOptions Options { get; }

    /// <summary>
    /// Gets the currently assigned partitions.
    /// </summary>
    public IReadOnlyList<KafkaTopicPartition> Assignment => _cluster.GetAssignment(GroupId, MemberId);

    /// <summary>
    /// Subscribes this member to one or more topics and triggers a deterministic group rebalance.
    /// </summary>
    public void Subscribe(params string[] topics)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cluster.Subscribe(GroupId, MemberId, topics);
    }

    /// <summary>
    /// Consumes the next available record from the currently owned partitions, or null when none are available.
    /// </summary>
    public async Task<KafkaRecord<T>?> ConsumeAsync<T>(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var record = await _cluster.ConsumeAsync(GroupId, MemberId, cancellationToken);

        if (record is null)
        {
            return null;
        }

        var value = JsonSerializer.Deserialize<T>(record.Value.Value.Span)
            ?? throw new InvalidOperationException("Kafka record payload deserialized to null.");
        return new KafkaRecord<T>(record.Partition.Topic, record.Partition.Partition, record.Value.Offset, record.Value.Key, value);
    }

    /// <summary>
    /// Stores the next offset locally without committing it to the group coordinator.
    /// </summary>
    public void StoreOffset<T>(KafkaRecord<T> record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cluster.StoreOffset(GroupId, MemberId, new KafkaTopicPartition(record.Topic, record.Partition), checked(record.Offset + 1));
    }

    /// <summary>
    /// Commits a consumed record's next offset immediately.
    /// </summary>
    public Task CommitAsync<T>(KafkaRecord<T> record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var partition = new KafkaTopicPartition(record.Topic, record.Partition);
        return _cluster.CommitAsync(GroupId, MemberId, partition, checked(record.Offset + 1), cancellationToken);
    }

    /// <summary>
    /// Commits all offsets previously stored by this consumer.
    /// </summary>
    public Task CommitStoredOffsetsAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _cluster.CommitStoredOffsetsAsync(GroupId, MemberId, cancellationToken);
    }

    /// <summary>
    /// Leaves the consumer group and deterministically reassigns its partitions.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cluster.Leave(GroupId, MemberId);
        }

        return ValueTask.CompletedTask;
    }
}
