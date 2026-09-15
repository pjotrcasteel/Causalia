namespace Causalia.Linearizability;

/// <summary>
/// Defines an executable sequential model used to validate a concurrent operation history.
/// </summary>
public sealed class LinearizabilitySpecification<TState, TInput, TOutput>
    where TState : notnull
{
    private readonly Func<TState, TInput, TOutput, LinearizabilityStep<TState>> _step;

    /// <summary>
    /// Initializes a sequential specification.
    /// </summary>
    public LinearizabilitySpecification(
        TState initialState,
        Func<TState, TInput, TOutput, LinearizabilityStep<TState>> step,
        IEqualityComparer<TState>? stateComparer = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        InitialState = initialState;
        _step = step;
        StateComparer = stateComparer ?? EqualityComparer<TState>.Default;
    }

    /// <summary>
    /// Gets the initial sequential model state.
    /// </summary>
    public TState InitialState { get; }

    /// <summary>
    /// Gets the comparer used to identify equivalent model states during search.
    /// </summary>
    public IEqualityComparer<TState> StateComparer { get; }

    internal LinearizabilityStep<TState> Step(TState state, TInput input, TOutput output)
    {
        return _step(state, input, output);
    }
}
