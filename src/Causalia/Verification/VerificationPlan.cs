namespace Causalia.Verification;

/// <summary>
/// Defines an ordered verification program that composes deterministic simulation, schedule exploration and model-based testing.
/// </summary>
public sealed class VerificationPlan
{
    internal VerificationPlan(string name, IReadOnlyList<VerificationStepDefinition> steps)
    {
        Name = name;
        Steps = steps;
    }

    /// <summary>
    /// Gets the stable plan name used in CI reports and fingerprints.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the number of ordered verification steps in the plan.
    /// </summary>
    public int StepCount => Steps.Count;

    internal IReadOnlyList<VerificationStepDefinition> Steps { get; }

    /// <summary>
    /// Creates an immutable verification plan using the supplied builder configuration.
    /// </summary>
    public static VerificationPlan Create(string name, Action<VerificationPlanBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new VerificationPlanBuilder();
        configure(builder);
        return builder.Build(name);
    }
}
