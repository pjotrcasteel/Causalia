namespace Causalia.Kafka;

/// <summary>
/// Simulates Kafka topics, partitions, consumer groups, rebalances and durable committed offsets.
/// </summary>
public sealed class SimulationKafkaCluster
{
    private readonly SimulationContext _context;
    private readonly Causalia.Faults.FaultInjector<KafkaCommitFaultContext, KafkaCommitFault>? _commitFaults;
    private readonly Dictionary<string, KafkaConsumerGroupState> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KafkaTopicState> _topics = new(StringComparer.Ordinal);

    internal SimulationKafkaCluster(SimulationContext context, string name, KafkaFaultPlan? faults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _context = context;
        Name = name;
        _commitFaults = faults is null
            ? null
            : context.CreateFaultInjector($"kafka:{name}:commit", faults.Plan);
        Trace($"kafka:cluster:created:{Name}");
    }

    /// <summary>
    /// Gets the logical cluster name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a topic with a fixed partition count.
    /// </summary>
    public void CreateTopic(string topic, int partitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (partitions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partitions), partitions, "Partition count must be greater than zero.");
        }

        if (_topics.ContainsKey(topic))
        {
            throw new InvalidOperationException($"Kafka topic '{topic}' already exists.");
        }

        var logs = Enumerable.Range(0, partitions).Select(_ => new List<KafkaStoredRecord>()).ToList().AsReadOnly();
        _topics.Add(topic, new KafkaTopicState { Name = topic, Partitions = logs });
        Trace($"kafka:topic:created:{Name}:{topic}:partitions:{partitions}");
    }

    /// <summary>
    /// Creates a deterministic producer.
    /// </summary>
    public SimulationKafkaProducer CreateProducer()
    {
        return new SimulationKafkaProducer(this);
    }

    /// <summary>
    /// Creates and joins one deterministic consumer-group member.
    /// </summary>
    public SimulationKafkaConsumer CreateConsumer(
        string groupId,
        string memberId,
        KafkaConsumerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        var group = GetOrCreateGroup(groupId);

        if (group.Members.ContainsKey(memberId))
        {
            throw new InvalidOperationException($"Kafka member '{memberId}' already exists in group '{groupId}'.");
        }

        group.Members.Add(memberId, new KafkaGroupMemberState
        {
            MemberId = memberId,
            Options = options ?? new KafkaConsumerOptions()
        });
        Trace($"kafka:group:joined:{Name}:{groupId}:{memberId}");
        Rebalance(group);
        return new SimulationKafkaConsumer(this, groupId, memberId, options ?? new KafkaConsumerOptions());
    }

    internal async Task<KafkaProduceResult> ProduceAsync(
        string topic,
        string? key,
        ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = GetTopic(topic);
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var partition = SelectPartition(state, key);
        var log = state.Partitions[partition];
        var offset = log.Count;
        log.Add(new KafkaStoredRecord(offset, key, value.ToArray()));
        Trace($"kafka:record:produced:{Name}:{topic}:{partition}:{offset}");
        return new KafkaProduceResult { Topic = topic, Partition = partition, Offset = offset };
    }

    internal void Subscribe(string groupId, string memberId, IReadOnlyList<string> topics)
    {
        var group = GetGroup(groupId);
        var member = GetMember(group, memberId);
        member.Topics.Clear();

        foreach (var topic in topics)
        {
            _ = GetTopic(topic);
            member.Topics.Add(topic);
        }

        Trace($"kafka:consumer:subscribed:{Name}:{groupId}:{memberId}:{string.Join(',', member.Topics.Order())}");
        Rebalance(group);
    }

    internal IReadOnlyList<KafkaTopicPartition> GetAssignment(string groupId, string memberId)
    {
        var member = GetMember(GetGroup(groupId), memberId);
        return member.Assignment.OrderBy(partition => partition.Topic, StringComparer.Ordinal)
            .ThenBy(partition => partition.Partition)
            .ToList()
            .AsReadOnly();
    }

    internal async Task<KafkaConsumedRecord?> ConsumeAsync(
        string groupId,
        string memberId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var group = GetGroup(groupId);
        var member = GetMember(group, memberId);
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveMember(group, member);

        foreach (var partition in member.Assignment.OrderBy(value => value.Topic, StringComparer.Ordinal).ThenBy(value => value.Partition))
        {
            var log = GetTopic(partition.Topic).Partitions[partition.Partition];
            var position = member.Positions[partition];

            if (position >= log.Count)
            {
                continue;
            }

            var record = log[checked((int)position)];
            member.Positions[partition] = checked(position + 1);
            Trace($"kafka:record:consumed:{Name}:{groupId}:{memberId}:{partition.Topic}:{partition.Partition}:{record.Offset}");
            return new KafkaConsumedRecord(partition, record);
        }

        return null;
    }

    internal void StoreOffset(string groupId, string memberId, KafkaTopicPartition partition, long offset)
    {
        var member = GetMember(GetGroup(groupId), memberId);
        RequireOwnership(member, partition);
        member.StoredOffsets[partition] = offset;
        Trace($"kafka:offset:stored:{Name}:{groupId}:{memberId}:{partition.Topic}:{partition.Partition}:{offset}");
    }

    internal async Task CommitAsync(
        string groupId,
        string memberId,
        KafkaTopicPartition partition,
        long offset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var group = GetGroup(groupId);
        var member = GetMember(group, memberId);
        var generation = group.Generation;
        RequireOwnership(member, partition);
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        RequireCommitOwnership(group, member, generation, partition);
        var faultContext = new KafkaCommitFaultContext
        {
            GroupId = groupId,
            MemberId = memberId,
            Partition = partition,
            Offset = offset
        };
        var acknowledgementLost = false;

        foreach (var fault in _commitFaults?.Evaluate(faultContext) ?? [])
        {
            switch (fault)
            {
                case KafkaCommitDelayFault delayed:
                    Trace($"kafka:offset:commit-delayed:{Name}:{groupId}:{partition.Topic}:{partition.Partition}:{delayed.Delay.Ticks}");
                    await Task.Delay(delayed.Delay, _context.TimeProvider, cancellationToken);
                    break;
                case KafkaCommitRejectedFault:
                    Trace($"kafka:offset:commit-rejected:{Name}:{groupId}:{memberId}:{partition.Topic}:{partition.Partition}:{offset}");
                    throw new SimulationKafkaCommitException("Kafka offset commit was rejected before it was applied.", false);
                case KafkaCommitAcknowledgementLostFault:
                    acknowledgementLost = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported Kafka commit fault '{fault.GetType().FullName}'.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        RequireCommitOwnership(group, member, generation, partition);
        group.CommittedOffsets[partition] = offset;
        Trace($"kafka:offset:committed:{Name}:{groupId}:{memberId}:{partition.Topic}:{partition.Partition}:{offset}");

        if (acknowledgementLost)
        {
            Trace($"kafka:offset:commit-ack-lost:{Name}:{groupId}:{memberId}:{partition.Topic}:{partition.Partition}:{offset}");
            throw new SimulationKafkaCommitException("Kafka offset commit succeeded but its acknowledgement was lost.", true);
        }
    }

    internal async Task CommitStoredOffsetsAsync(string groupId, string memberId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var group = GetGroup(groupId);
        var member = GetMember(group, memberId);

        foreach (var stored in member.StoredOffsets.ToList())
        {
            RequireActiveMember(group, member);
            await CommitAsync(groupId, memberId, stored.Key, stored.Value, cancellationToken);

            if (member.StoredOffsets.TryGetValue(stored.Key, out var currentOffset) && currentOffset == stored.Value)
            {
                member.StoredOffsets.Remove(stored.Key);
            }
        }
    }

    internal void Leave(string groupId, string memberId)
    {
        var group = GetGroup(groupId);

        if (!group.Members.Remove(memberId))
        {
            return;
        }

        Trace($"kafka:group:left:{Name}:{groupId}:{memberId}");
        Rebalance(group);
    }

    private void Rebalance(KafkaConsumerGroupState group)
    {
        group.Generation = checked(group.Generation + 1);
        foreach (var member in group.Members.Values)
        {
            member.Assignment.Clear();
            member.Positions.Clear();
            member.StoredOffsets.Clear();
        }

        foreach (var topicName in group.Members.Values.SelectMany(member => member.Topics).Distinct(StringComparer.Ordinal).Order())
        {
            var subscribed = group.Members.Values.Where(member => member.Topics.Contains(topicName)).OrderBy(member => member.MemberId).ToList();

            if (subscribed.Count == 0)
            {
                continue;
            }

            var topic = GetTopic(topicName);

            for (var partitionIndex = 0; partitionIndex < topic.Partitions.Count; partitionIndex++)
            {
                var member = subscribed[partitionIndex % subscribed.Count];
                var partition = new KafkaTopicPartition(topicName, partitionIndex);
                member.Assignment.Add(partition);
                member.Positions[partition] = ResolveInitialPosition(group, member, partition, topic.Partitions[partitionIndex].Count);
            }
        }

        Trace($"kafka:group:rebalanced:{Name}:{group.GroupId}:members:{group.Members.Count}");

        foreach (var member in group.Members.Values.OrderBy(member => member.MemberId))
        {
            var assigned = string.Join(',', member.Assignment.OrderBy(value => value.Topic).ThenBy(value => value.Partition)
                .Select(value => $"{value.Topic}[{value.Partition}]"));
            Trace($"kafka:assignment:{Name}:{group.GroupId}:{member.MemberId}:{assigned}");
        }
    }

    private static long ResolveInitialPosition(
        KafkaConsumerGroupState group,
        KafkaGroupMemberState member,
        KafkaTopicPartition partition,
        int logCount)
    {
        if (group.CommittedOffsets.TryGetValue(partition, out var committed))
        {
            return committed;
        }

        return member.Options.AutoOffsetReset == KafkaOffsetReset.Earliest ? 0 : logCount;
    }

    private static void RequireOwnership(KafkaGroupMemberState member, KafkaTopicPartition partition)
    {
        if (!member.Assignment.Contains(partition))
        {
            throw new SimulationKafkaPartitionOwnershipException(member.MemberId, partition);
        }
    }

    private static void RequireActiveMember(KafkaConsumerGroupState group, KafkaGroupMemberState member)
    {
        if (!group.Members.TryGetValue(member.MemberId, out var current) || !ReferenceEquals(member, current))
        {
            throw new ObjectDisposedException(nameof(SimulationKafkaConsumer));
        }
    }

    private static void RequireCommitOwnership(KafkaConsumerGroupState group, KafkaGroupMemberState member, long generation, KafkaTopicPartition partition)
    {
        if (group.Generation != generation
            || !group.Members.TryGetValue(member.MemberId, out var current)
            || !ReferenceEquals(member, current))
        {
            throw new SimulationKafkaPartitionOwnershipException(member.MemberId, partition);
        }

        RequireOwnership(member, partition);
    }

    private int SelectPartition(KafkaTopicState topic, string? key)
    {
        if (key is not null)
        {
            return (int)(StableHash(key) % (uint)topic.Partitions.Count);
        }

        var selected = topic.NextUnkeyedPartition;
        topic.NextUnkeyedPartition = (selected + 1) % topic.Partitions.Count;
        return selected;
    }

    private KafkaConsumerGroupState GetOrCreateGroup(string groupId)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            return group;
        }

        group = new KafkaConsumerGroupState { GroupId = groupId };
        _groups.Add(groupId, group);
        return group;
    }

    private KafkaConsumerGroupState GetGroup(string groupId)
    {
        return _groups.TryGetValue(groupId, out var group)
            ? group
            : throw new InvalidOperationException($"Kafka consumer group '{groupId}' does not exist.");
    }

    private KafkaTopicState GetTopic(string topic)
    {
        return _topics.TryGetValue(topic, out var state)
            ? state
            : throw new InvalidOperationException($"Kafka topic '{topic}' does not exist.");
    }

    private static KafkaGroupMemberState GetMember(KafkaConsumerGroupState group, string memberId)
    {
        return group.Members.TryGetValue(memberId, out var member)
            ? member
            : throw new InvalidOperationException($"Kafka member '{memberId}' does not exist in group '{group.GroupId}'.");
    }

    private static uint StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;

        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private void Trace(string message)
    {
        _context.TraceEvent(message);
    }
}
