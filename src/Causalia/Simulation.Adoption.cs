namespace Causalia;

public static partial class Simulation
{
    /// <summary>
    /// Executes one scenario using the default deterministic simulation options.
    /// </summary>
    public static Task<SimulationResult> RunAsync(
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return RunAsync(new SimulationOptions(), scenario, cancellationToken);
    }
}
