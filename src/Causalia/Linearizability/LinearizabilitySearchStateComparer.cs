namespace Causalia.Linearizability;

internal sealed class LinearizabilitySearchStateComparer<TState> : IEqualityComparer<LinearizabilitySearchState<TState>>
    where TState : notnull
{
    private readonly IEqualityComparer<TState> _stateComparer;

    public LinearizabilitySearchStateComparer(IEqualityComparer<TState> stateComparer)
    {
        _stateComparer = stateComparer;
    }

    public bool Equals(LinearizabilitySearchState<TState>? x, LinearizabilitySearchState<TState>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x is not null && y is not null && x.Linearized == y.Linearized && _stateComparer.Equals(x.State, y.State);
    }

    public int GetHashCode(LinearizabilitySearchState<TState> obj)
    {
        return HashCode.Combine(obj.Linearized, _stateComparer.GetHashCode(obj.State));
    }
}
