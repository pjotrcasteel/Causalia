namespace Causalia.Faults;

/// <summary>
/// Evaluates whether one deterministic fault effect should be applied for a simulation event.
/// </summary>
public interface IFaultPolicy<in TContext, TEffect>
{
    /// <summary>
    /// Gets the human-readable policy name used in diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the policy for one event occurrence.
    /// </summary>
    bool TryEvaluate(TContext context, FaultEvaluationContext evaluationContext, out TEffect effect);
}
