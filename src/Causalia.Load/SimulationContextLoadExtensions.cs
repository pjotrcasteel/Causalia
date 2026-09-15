namespace Causalia.Load;

/// <summary>
/// Adds deterministic load execution to a simulation context.
/// </summary>
public static class SimulationContextLoadExtensions
{
    /// <summary>
    /// Creates a deterministic load runner bound to the supplied simulation context.
    /// </summary>
    public static SimulationLoadRunner CreateLoadRunner(this SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimulationLoadRunner(context);
    }
}
