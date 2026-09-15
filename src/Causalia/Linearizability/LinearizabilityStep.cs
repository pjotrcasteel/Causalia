namespace Causalia.Linearizability;

/// <summary>
/// Describes whether one observed operation is legal in a sequential specification and, if so, the next model state.
/// </summary>
public readonly record struct LinearizabilityStep<TState>(bool IsValid, TState State)
{
    /// <summary>
    /// Creates a valid transition to the supplied state.
    /// </summary>
    public static LinearizabilityStep<TState> Accept(TState state)
    {
        return new LinearizabilityStep<TState>(true, state);
    }

    /// <summary>
    /// Rejects the observed operation for the current model state.
    /// </summary>
    public static LinearizabilityStep<TState> Reject(TState state)
    {
        return new LinearizabilityStep<TState>(false, state);
    }
}
