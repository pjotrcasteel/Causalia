namespace Causalia.Processes;

/// <summary>
/// Represents the lifecycle state of a simulated process.
/// </summary>
public enum SimulationProcessState
{
    /// <summary>
    /// A generation is being created and started.
    /// </summary>
    Starting,

    /// <summary>
    /// The current generation is running.
    /// </summary>
    Running,

    /// <summary>
    /// The current generation is performing graceful shutdown.
    /// </summary>
    Stopping,

    /// <summary>
    /// The process was stopped gracefully and has no live generation.
    /// </summary>
    Stopped,

    /// <summary>
    /// The process crashed and has no live generation.
    /// </summary>
    Crashed,

    /// <summary>
    /// A replacement generation is being created after stop or crash.
    /// </summary>
    Restarting
}
