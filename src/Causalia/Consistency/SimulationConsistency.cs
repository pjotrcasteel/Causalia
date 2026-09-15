using Causalia.Runtime;

namespace Causalia.Consistency;

/// <summary>
/// Creates deterministic histories for distributed-consistency verification.
/// </summary>
public sealed class SimulationConsistency
{
    private readonly HashSet<string> _historyNames = new(StringComparer.Ordinal);
    private readonly List<IConsistencyHistory> _histories = new();
    private readonly DeterministicScheduler _scheduler;

    internal SimulationConsistency(DeterministicScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>
    /// Creates a logical read/write history whose observations can be checked against distributed-consistency guarantees.
    /// </summary>
    public ConsistencyHistory<TKey> CreateHistory<TKey>(string name, IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_historyNames.Add(name))
        {
            throw new InvalidOperationException($"A consistency history named '{name}' already exists in this simulation run.");
        }

        var history = new ConsistencyHistory<TKey>(name, _scheduler, keyComparer ?? EqualityComparer<TKey>.Default);
        _histories.Add(history);
        return history;
    }

    internal IReadOnlyList<ConsistencyOutcome> Complete()
    {
        return _histories.Select(history => history.Complete()).ToList().AsReadOnly();
    }
}
