namespace Causalia.Minimization;

/// <summary>
/// Configures deterministic failure minimization.
/// </summary>
public sealed class MinimizationOptions
{
    /// <summary>
    /// Gets or initializes the maximum number of candidate reproductions Causalia may execute.
    /// </summary>
    public int MaxAttempts { get; init; } = 1_000;

    /// <summary>
    /// Gets or initializes whether non-canonical scheduler choices may be removed.
    /// </summary>
    public bool MinimizeSchedulerChoices { get; init; } = true;

    /// <summary>
    /// Gets or initializes whether triggered fault occurrences may be removed.
    /// </summary>
    public bool MinimizeFaults { get; init; } = true;
}
