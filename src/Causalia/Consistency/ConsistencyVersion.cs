namespace Causalia.Consistency;

/// <summary>
/// Identifies one logical write recorded in a consistency history.
/// </summary>
/// <typeparam name="TKey">The logical key type used by the history.</typeparam>
public sealed class ConsistencyVersion<TKey>
    where TKey : notnull
{
    internal ConsistencyVersion(long id, TKey key, string sessionId, string replicaId, DateTimeOffset writtenAt, ConsistencyVersionMetadata metadata)
    {
        Id = id;
        Key = key;
        SessionId = sessionId;
        ReplicaId = replicaId;
        WrittenAt = writtenAt;
        CausalPredecessorIds = Array.AsReadOnly(metadata.CausalPredecessorIds.ToArray());
        ReadPredecessorIds = Array.AsReadOnly(metadata.ReadPredecessorIds.ToArray());
        SessionWriteSequence = metadata.SessionWriteSequence;
    }

    /// <summary>
    /// Gets the stable write identifier within this history.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// Gets the logical key written by this version.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the session that performed the write.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the replica against which the write was performed.
    /// </summary>
    public string ReplicaId { get; }

    /// <summary>
    /// Gets the virtual timestamp at which the write was recorded.
    /// </summary>
    public DateTimeOffset WrittenAt { get; }

    /// <summary>
    /// Gets the complete causal predecessor closure known to the writing session.
    /// </summary>
    public IReadOnlyList<long> CausalPredecessorIds { get; }

    internal IReadOnlyList<long> ReadPredecessorIds { get; }

    internal int SessionWriteSequence { get; }
}
