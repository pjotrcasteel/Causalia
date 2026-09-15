namespace Causalia.Exceptions;

/// <summary>
/// Indicates that Causalia could not reproduce the supplied failure before attempting minimization.
/// </summary>
public sealed class SimulationMinimizationException : Exception
{
    internal SimulationMinimizationException(string message)
        : base(message)
    {
    }
}
