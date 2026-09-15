namespace Causalia.Consistency;

/// <summary>
/// Tracks the logical consistency state accumulated by one client session.
/// </summary>
internal sealed class ConsistencySessionState<TKey>
    where TKey : notnull
{
    public ConsistencySessionState(IEqualityComparer<TKey> comparer)
    {
        LastWrites = new Dictionary<TKey, ConsistencyVersion<TKey>>(comparer);
        LastReads = new Dictionary<TKey, ConsistencyVersion<TKey>?>(comparer);
    }

    public Dictionary<TKey, ConsistencyVersion<TKey>> LastWrites { get; }

    public Dictionary<TKey, ConsistencyVersion<TKey>?> LastReads { get; }

    public HashSet<long> CausalVersionIds { get; } = new();

    public HashSet<long> ReadVersionIds { get; } = new();

    public int Writes { get; set; }
}
