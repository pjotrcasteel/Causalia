using System.Globalization;

namespace Causalia.Load.Thresholds;

internal static class LoadThresholdEvaluator
{
    public static IReadOnlyList<LoadThresholdViolation> Evaluate(LoadRunResult result, LoadThresholds? thresholds)
    {
        if (thresholds is null)
        {
            return Array.Empty<LoadThresholdViolation>();
        }

        var violations = new List<LoadThresholdViolation>();
        AddRateViolation(
            violations,
            "maximum-failure-rate",
            result.FailureRate,
            thresholds.MaximumFailureRate);
        AddRateViolation(
            violations,
            "maximum-dropped-rate",
            result.DroppedRate,
            thresholds.MaximumDroppedRate);
        AddDurationViolation(
            violations,
            "maximum-p95",
            result.Latency.Percentile95,
            thresholds.MaximumPercentile95);
        AddDurationViolation(
            violations,
            "maximum-p99",
            result.Latency.Percentile99,
            thresholds.MaximumPercentile99);

        if (thresholds.MaximumPeakConcurrency is { } maximumPeak && result.PeakConcurrency > maximumPeak)
        {
            violations.Add(
                new LoadThresholdViolation(
                    "maximum-peak-concurrency",
                    result.PeakConcurrency.ToString(CultureInfo.InvariantCulture),
                    maximumPeak.ToString(CultureInfo.InvariantCulture)));
        }

        return violations.AsReadOnly();
    }

    private static void AddRateViolation(
        ICollection<LoadThresholdViolation> violations,
        string name,
        double observed,
        double? maximum)
    {
        if (maximum is null || observed <= maximum.Value)
        {
            return;
        }

        violations.Add(
            new LoadThresholdViolation(
                name,
                observed.ToString("R", CultureInfo.InvariantCulture),
                maximum.Value.ToString("R", CultureInfo.InvariantCulture)));
    }

    private static void AddDurationViolation(
        ICollection<LoadThresholdViolation> violations,
        string name,
        TimeSpan observed,
        TimeSpan? maximum)
    {
        if (maximum is null || observed <= maximum.Value)
        {
            return;
        }

        violations.Add(
            new LoadThresholdViolation(
                name,
                observed.Ticks.ToString(CultureInfo.InvariantCulture),
                maximum.Value.Ticks.ToString(CultureInfo.InvariantCulture)));
    }
}
