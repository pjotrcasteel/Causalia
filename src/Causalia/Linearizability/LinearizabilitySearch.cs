namespace Causalia.Linearizability;

internal sealed class LinearizabilitySearch<TState, TInput, TOutput>
    where TState : notnull
{
    private static readonly IReadOnlyList<long> EmptyOperationIds = new List<long>().AsReadOnly();
    private readonly IReadOnlyList<LinearizabilityOperation<TInput, TOutput>> _operations;
    private readonly IReadOnlyList<ulong> _predecessors;
    private readonly LinearizabilitySpecification<TState, TInput, TOutput> _specification;
    private readonly HashSet<LinearizabilitySearchState<TState>> _visited;
    private readonly int _maxSearchStates;
    private readonly List<int> _path = new();
    private List<long> _longestPrefix = new();
    private IReadOnlyList<long> _linearization = EmptyOperationIds;
    private bool _budgetExhausted;
    private int _searchStates;

    public LinearizabilitySearch(
        IReadOnlyList<LinearizabilityOperation<TInput, TOutput>> operations,
        IReadOnlyList<ulong> predecessors,
        LinearizabilitySpecification<TState, TInput, TOutput> specification,
        int maxSearchStates)
    {
        _operations = operations;
        _predecessors = predecessors;
        _specification = specification;
        _maxSearchStates = maxSearchStates;
        _visited = new HashSet<LinearizabilitySearchState<TState>>(
            new LinearizabilitySearchStateComparer<TState>(specification.StateComparer));
    }

    public LinearizabilityResult Run(int pendingOperations)
    {
        var allOperations = (1UL << _operations.Count) - 1;
        var found = Visit(0, _specification.InitialState, allOperations);

        if (found)
        {
            return new LinearizabilityResult(
                LinearizabilityStatus.Linearizable,
                new LinearizabilitySearchMetrics(_operations.Count, pendingOperations, _searchStates),
                _linearization,
                _longestPrefix.AsReadOnly(),
                null);
        }

        if (_budgetExhausted)
        {
            return new LinearizabilityResult(
                LinearizabilityStatus.Inconclusive,
                new LinearizabilitySearchMetrics(_operations.Count, pendingOperations, _searchStates),
                EmptyOperationIds,
                _longestPrefix.AsReadOnly(),
                $"Search reached MaxSearchStates={_maxSearchStates} before proving or disproving linearizability.");
        }

        return new LinearizabilityResult(
            LinearizabilityStatus.NotLinearizable,
            new LinearizabilitySearchMetrics(_operations.Count, pendingOperations, _searchStates),
            EmptyOperationIds,
            _longestPrefix.AsReadOnly(),
            null);
    }

    private bool Visit(ulong linearized, TState state, ulong allOperations)
    {
        if (linearized == allOperations)
        {
            _linearization = _path.Select(index => _operations[index].Id).ToList().AsReadOnly();
            return true;
        }

        if (_searchStates >= _maxSearchStates)
        {
            _budgetExhausted = true;
            return false;
        }

        var searchState = new LinearizabilitySearchState<TState>(linearized, state);

        if (!_visited.Add(searchState))
        {
            return false;
        }

        _searchStates++;
        CaptureLongestPrefix();

        for (var index = 0; index < _operations.Count; index++)
        {
            var bit = 1UL << index;

            if ((linearized & bit) != 0 || (_predecessors[index] & ~linearized) != 0)
            {
                continue;
            }

            var operation = _operations[index];
            var transition = _specification.Step(state, operation.Input, operation.Output);

            if (!transition.IsValid)
            {
                continue;
            }

            _path.Add(index);

            if (Visit(linearized | bit, transition.State, allOperations))
            {
                return true;
            }

            _path.RemoveAt(_path.Count - 1);

            if (_budgetExhausted)
            {
                return false;
            }
        }

        return false;
    }

    private void CaptureLongestPrefix()
    {
        if (_path.Count <= _longestPrefix.Count)
        {
            return;
        }

        _longestPrefix = _path.Select(index => _operations[index].Id).ToList();
    }
}
