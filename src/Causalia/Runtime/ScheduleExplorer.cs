using Causalia.Scheduling;

namespace Causalia.Runtime;

internal static class ScheduleExplorer
{
    public static IReadOnlyList<int>? CreateNextPrefix(SimulationSchedule schedule, int maxDecisionDepth)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var boundedCount = Math.Min(schedule.Decisions.Count, maxDecisionDepth);

        for (var index = boundedCount - 1; index >= 0; index--)
        {
            var decision = schedule.Decisions[index];

            if (decision.SelectedIndex + 1 >= decision.CandidateCount)
            {
                continue;
            }

            var prefix = new int[index + 1];

            for (var prefixIndex = 0; prefixIndex < index; prefixIndex++)
            {
                prefix[prefixIndex] = schedule.Decisions[prefixIndex].SelectedIndex;
            }

            prefix[index] = decision.SelectedIndex + 1;
            return prefix;
        }

        return null;
    }
}
