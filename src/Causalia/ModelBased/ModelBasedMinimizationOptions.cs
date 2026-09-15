namespace Causalia.ModelBased;

/// <summary>
/// Configures deterministic command-sequence minimization after a model-based failure.
/// </summary>
public sealed class ModelBasedMinimizationOptions
{
    /// <summary>
    /// Gets or initializes the maximum number of candidate command sequences executed while shrinking.
    /// </summary>
    public int MaxAttempts { get; init; } = 500;
}
