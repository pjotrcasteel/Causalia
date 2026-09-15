namespace Causalia.Nodes;

/// <summary>
/// Represents the lifecycle state of a simulated node.
/// </summary>
public enum SimulationNodeState
{
    /// <summary>
    /// The node is running and can execute work.
    /// </summary>
    Running,

    /// <summary>
    /// The node was stopped gracefully and cannot execute work until restarted.
    /// </summary>
    Stopped,

    /// <summary>
    /// The node is crashed and cannot execute work until restarted.
    /// </summary>
    Crashed
}
