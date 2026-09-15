namespace Causalia.Linearizability;

internal sealed record LinearizabilitySearchState<TState>(ulong Linearized, TState State)
    where TState : notnull;
