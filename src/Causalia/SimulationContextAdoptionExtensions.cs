namespace Causalia;

/// <summary>
/// Provides concise golden-path APIs for common simulation scenarios.
/// </summary>
public static class SimulationContextAdoptionExtensions
{
    /// <summary>
    /// Requires the supplied condition to remain true for the complete simulation run.
    /// </summary>
    public static void Invariant(this SimulationContext context, string name, Func<bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(predicate);
        context.Invariants.Always(name, predicate);
    }
}
