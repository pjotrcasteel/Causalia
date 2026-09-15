namespace Causalia.ModelBased;

/// <summary>
/// Reports that a persisted model command sequence no longer matches the executable model shape.
/// </summary>
public sealed class SimulationModelReplayException : Exception
{
    internal SimulationModelReplayException(string message)
        : base(message)
    {
    }
}
