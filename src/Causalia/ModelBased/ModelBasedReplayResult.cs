namespace Causalia.ModelBased;

/// <summary>
/// Describes a successfully replayed model command sequence.
/// </summary>
public sealed class ModelBasedReplayResult<TState>
{
    internal ModelBasedReplayResult(ModelSequence sequence, TState finalState, SimulationResult result)
    {
        Sequence = sequence;
        FinalState = finalState;
        Simulation = result;
    }

    /// <summary>
    /// Gets the exact model command sequence that was replayed.
    /// </summary>
    public ModelSequence Sequence { get; }

    /// <summary>
    /// Gets the final model state derived from the replayed command transitions.
    /// </summary>
    public TState FinalState { get; }

    /// <summary>
    /// Gets the completed deterministic simulation result.
    /// </summary>
    public SimulationResult Simulation { get; }
}
