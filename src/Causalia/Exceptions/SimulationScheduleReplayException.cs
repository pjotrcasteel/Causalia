namespace Causalia.Exceptions;

/// <summary>
/// Indicates that a stored schedule can no longer be replayed against the current deterministic execution shape.
/// </summary>
public sealed class SimulationScheduleReplayException : Exception
{
    internal SimulationScheduleReplayException(string message)
        : base(message)
    {
    }
}
