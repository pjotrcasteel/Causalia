namespace Causalia.Scheduling;

/// <summary>
/// Selects how Causalia prioritizes bounded scheduler prefixes during exploration.
/// </summary>
public enum ExplorationStrategy
{
    /// <summary>
    /// Explores schedules using deterministic bounded depth-first backtracking.
    /// </summary>
    DepthFirst,

    /// <summary>
    /// Prioritizes unexplored schedule prefixes descending from runs that discovered new coverage points.
    /// </summary>
    CoverageGuided,

    /// <summary>
    /// Uses conservative operation-level dynamic partial-order reduction to skip schedules proven equivalent by declared resource access.
    /// </summary>
    DynamicPartialOrderReduction
}
