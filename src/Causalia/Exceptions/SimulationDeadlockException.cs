namespace Causalia.Exceptions;

/// <summary>
/// Indicates that a simulation has incomplete work but no runnable work or virtual timer remains.
/// </summary>
public sealed class SimulationDeadlockException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationDeadlockException"/> class.
    /// </summary>
    public SimulationDeadlockException()
        : base("The simulation cannot make progress because no runnable work or virtual timer remains.")
    {
    }
}
