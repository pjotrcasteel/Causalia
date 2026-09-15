using Causalia.Scheduling;

namespace Causalia.Runtime;

internal sealed class CoverageGuidedFrontier
{
    private readonly HashSet<string> _knownPrefixes = new(StringComparer.Ordinal) { string.Empty };
    private readonly PriorityQueue<IReadOnlyList<int>, (int NegativeScore, long Sequence)> _queue = new();
    private long _sequence;

    public int Count => _queue.Count;

    public void AddAlternatives(SimulationSchedule schedule, int maxDecisionDepth, int noveltyScore)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var boundedCount = Math.Min(schedule.Decisions.Count, maxDecisionDepth);

        for (var decisionIndex = 0; decisionIndex < boundedCount; decisionIndex++)
        {
            var decision = schedule.Decisions[decisionIndex];

            for (var selectedIndex = 0; selectedIndex < decision.CandidateCount; selectedIndex++)
            {
                if (selectedIndex == decision.SelectedIndex)
                {
                    continue;
                }

                var prefix = NormalizePrefix(CreatePrefix(schedule, decisionIndex, selectedIndex));
                var key = CreateKey(prefix);

                if (!_knownPrefixes.Add(key))
                {
                    continue;
                }

                _queue.Enqueue(prefix, (-noveltyScore, _sequence++));
            }
        }
    }

    public IReadOnlyList<int> Dequeue()
    {
        return _queue.Dequeue();
    }

    private static IReadOnlyList<int> CreatePrefix(SimulationSchedule schedule, int decisionIndex, int selectedIndex)
    {
        var prefix = new List<int>(decisionIndex + 1);

        for (var index = 0; index < decisionIndex; index++)
        {
            prefix.Add(schedule.Decisions[index].SelectedIndex);
        }

        prefix.Add(selectedIndex);
        return prefix.AsReadOnly();
    }

    private static IReadOnlyList<int> NormalizePrefix(IReadOnlyList<int> prefix)
    {
        var count = prefix.Count;

        while (count > 0 && prefix[count - 1] == 0)
        {
            count--;
        }

        if (count == prefix.Count)
        {
            return prefix;
        }

        if (count == 0)
        {
            return Array.Empty<int>();
        }

        var normalized = new List<int>(count);

        for (var index = 0; index < count; index++)
        {
            normalized.Add(prefix[index]);
        }

        return normalized.AsReadOnly();
    }

    private static string CreateKey(IReadOnlyList<int> prefix)
    {
        return prefix.Count == 0 ? string.Empty : string.Join(',', prefix);
    }
}
