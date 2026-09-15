namespace Causalia.Load.Thresholds;

/// <summary>
/// Indicates that a completed deterministic load run exceeded one or more configured thresholds.
/// </summary>
public sealed class SimulationLoadThresholdException : Exception
{
    internal SimulationLoadThresholdException(LoadRunResult result, IReadOnlyList<LoadThresholdViolation> violations)
        : base(CreateMessage(violations))
    {
        Result = result;
        Violations = violations;
    }

    /// <summary>
    /// Gets the completed load result that violated the configured thresholds.
    /// </summary>
    public LoadRunResult Result { get; }

    /// <summary>
    /// Gets all deterministic threshold violations.
    /// </summary>
    public IReadOnlyList<LoadThresholdViolation> Violations { get; }

    private static string CreateMessage(IReadOnlyList<LoadThresholdViolation> violations)
    {
        var names = string.Join(",", violations.Select(violation => violation.Threshold));
        return $"Deterministic load thresholds exceeded: {names}.";
    }
}
