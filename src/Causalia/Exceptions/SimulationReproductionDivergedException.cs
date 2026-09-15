namespace Causalia.Exceptions;

/// <summary>
/// Indicates that a minimized reproduction no longer matches the execution shape required by its forced choices.
/// </summary>
public sealed class SimulationReproductionDivergedException : Exception
{
    internal SimulationReproductionDivergedException(string message)
        : base(message)
    {
    }
}
