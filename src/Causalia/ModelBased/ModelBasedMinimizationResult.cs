using Causalia.Exceptions;

namespace Causalia.ModelBased;

/// <summary>
/// Describes a command-sequence failure reduced by deterministic delta debugging.
/// </summary>
public sealed class ModelBasedMinimizationResult
{
    internal ModelBasedMinimizationResult(
        ModelSequence originalSequence,
        ModelSequence minimizedSequence,
        int attempts,
        SimulationFailedException failure)
    {
        OriginalSequence = originalSequence;
        MinimizedSequence = minimizedSequence;
        Attempts = attempts;
        Failure = failure;
    }

    /// <summary>
    /// Gets the original failing command sequence.
    /// </summary>
    public ModelSequence OriginalSequence { get; }

    /// <summary>
    /// Gets the smallest reproducing sequence found within the configured attempt bound.
    /// </summary>
    public ModelSequence MinimizedSequence { get; }

    /// <summary>
    /// Gets the number of candidate sequences executed while shrinking.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Gets the deterministic failure reproduced by the minimized sequence.
    /// </summary>
    public SimulationFailedException Failure { get; }
}
