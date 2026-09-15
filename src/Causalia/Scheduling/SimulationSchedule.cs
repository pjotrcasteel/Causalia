using System.Globalization;

namespace Causalia.Scheduling;

/// <summary>
/// Captures the branching decisions required to replay one deterministic execution schedule.
/// </summary>
public sealed class SimulationSchedule
{
    private const int MaximumTokenLength = 1_048_576;
    private const string TokenPrefix = "v1:";

    internal SimulationSchedule(IReadOnlyList<ScheduleDecision> decisions)
    {
        Decisions = decisions;
    }

    /// <summary>
    /// Gets the ordered branching decisions that define this execution schedule.
    /// </summary>
    public IReadOnlyList<ScheduleDecision> Decisions { get; }

    /// <summary>
    /// Gets a stable text token that can be logged and parsed later for replay.
    /// </summary>
    public string ReplayToken => ToString();

    /// <summary>
    /// Parses a replay token previously produced by <see cref="ReplayToken"/>.
    /// </summary>
    public static SimulationSchedule Parse(string replayToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayToken);

        if (replayToken.Length > MaximumTokenLength)
        {
            throw new ArgumentException($"Schedule replay tokens cannot exceed {MaximumTokenLength} characters.", nameof(replayToken));
        }

        if (!replayToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            throw new FormatException("The schedule replay token has an unsupported format or version.");
        }

        var payload = replayToken[TokenPrefix.Length..];

        if (payload.Length == 0)
        {
            return new SimulationSchedule(Array.Empty<ScheduleDecision>());
        }

        var encodedDecisions = payload.Split(';', StringSplitOptions.None);
        var decisions = new List<ScheduleDecision>(encodedDecisions.Length);

        for (var decisionIndex = 0; decisionIndex < encodedDecisions.Length; decisionIndex++)
        {
            var values = encodedDecisions[decisionIndex].Split(',', StringSplitOptions.None);

            if (values.Length != 4)
            {
                throw new FormatException("The schedule replay token contains an invalid decision.");
            }

            var step = ParseInt32(values[0]);
            var selectedIndex = ParseInt32(values[1]);
            var candidateCount = ParseInt32(values[2]);
            var workItemId = ParseInt64(values[3]);

            if (step <= 0 || selectedIndex < 0 || candidateCount <= 1 || selectedIndex >= candidateCount || workItemId < 0)
            {
                throw new FormatException("The schedule replay token contains an invalid scheduler decision.");
            }

            decisions.Add(new ScheduleDecision(decisionIndex, step, selectedIndex, candidateCount, workItemId));
        }

        return new SimulationSchedule(decisions.AsReadOnly());
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Decisions.Count == 0)
        {
            return TokenPrefix;
        }

        return TokenPrefix + string.Join(
            ";",
            Decisions.Select(
                decision => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{decision.Step},{decision.SelectedIndex},{decision.CandidateCount},{decision.WorkItemId}")));
    }

    private static int ParseInt32(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException("The schedule replay token contains an invalid integer value.");
        }

        return parsed;
    }

    private static long ParseInt64(string value)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException("The schedule replay token contains an invalid integer value.");
        }

        return parsed;
    }
}
