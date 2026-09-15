namespace Causalia.Exploration;

/// <summary>
/// Describes how a deterministic exploration operation accesses a logical shared resource.
/// </summary>
public enum ExplorationAccessKind
{
    /// <summary>
    /// Reads a resource without modifying it.
    /// </summary>
    Read,

    /// <summary>
    /// Writes or otherwise mutates a resource.
    /// </summary>
    Write,

    /// <summary>
    /// Synchronizes through a resource and therefore conflicts with every other access to that resource.
    /// </summary>
    Synchronize
}
