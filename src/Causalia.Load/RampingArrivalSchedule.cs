using Causalia.Load.Profiles;

namespace Causalia.Load;

internal static class RampingArrivalSchedule
{
    public static IReadOnlyList<TimeSpan> Create(RampingArrivalRateLoadProfile profile, long maximumIterations = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (maximumIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumIterations), maximumIterations, "Maximum iterations must be greater than zero.");
        }

        var offsets = new List<TimeSpan>();
        var stageStartTicks = 0L;
        var cumulative = 0m;
        var threshold = 0.5m;
        var startRate = profile.StartRate;

        foreach (var stage in profile.Stages)
        {
            var stageArea = CalculateArea(stage.Duration.Ticks, startRate, stage.TargetRate, profile.TimeUnit.Ticks);
            var stageEndCumulative = cumulative + stageArea;

            while (threshold < stageEndCumulative || threshold == stageEndCumulative && stage.TargetRate > 0)
            {
                if (offsets.Count >= maximumIterations)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(maximumIterations),
                        maximumIterations,
                        $"The ramping profile schedules more than {maximumIterations} iterations.");
                }

                var targetWithinStage = threshold - cumulative;
                var ticks = FindOffsetTicks(
                    stage.Duration.Ticks,
                    startRate,
                    stage.TargetRate,
                    profile.TimeUnit.Ticks,
                    targetWithinStage);

                offsets.Add(TimeSpan.FromTicks(checked(stageStartTicks + ticks)));
                threshold += 1m;
            }

            cumulative = stageEndCumulative;
            stageStartTicks = checked(stageStartTicks + stage.Duration.Ticks);
            startRate = stage.TargetRate;
        }

        return offsets.AsReadOnly();
    }

    private static long FindOffsetTicks(
        long durationTicks,
        int startRate,
        int targetRate,
        long timeUnitTicks,
        decimal targetArea)
    {
        var low = 0L;
        var high = durationTicks;

        while (low < high)
        {
            var midpoint = low + (high - low) / 2;
            var area = CalculateArea(midpoint, durationTicks, startRate, targetRate, timeUnitTicks);

            if (area >= targetArea)
            {
                high = midpoint;
            }
            else
            {
                low = midpoint + 1;
            }
        }

        return low;
    }

    private static decimal CalculateArea(long durationTicks, int startRate, int targetRate, long timeUnitTicks)
    {
        return CalculateArea(durationTicks, durationTicks, startRate, targetRate, timeUnitTicks);
    }

    private static decimal CalculateArea(
        long elapsedTicks,
        long durationTicks,
        int startRate,
        int targetRate,
        long timeUnitTicks)
    {
        if (elapsedTicks == 0)
        {
            return 0m;
        }

        var elapsed = (decimal)elapsedTicks;
        var duration = (decimal)durationTicks;
        var delta = targetRate - startRate;
        var integratedRate = startRate * elapsed + delta * elapsed * elapsed / (2m * duration);
        return integratedRate / timeUnitTicks;
    }
}
