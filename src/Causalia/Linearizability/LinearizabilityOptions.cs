namespace Causalia.Linearizability;

/// <summary>
/// Controls bounded linearizability search.
/// </summary>
public sealed class LinearizabilityOptions
{
    /// <summary>
    /// Gets or sets the maximum number of distinct search states that may be explored.
    /// </summary>
    public int MaxSearchStates { get; init; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum number of completed operations accepted by the bounded checker.
    /// </summary>
    public int MaxOperations { get; init; } = 63;
}
