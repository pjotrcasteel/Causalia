namespace Causalia.Linearizability;

internal static class LinearizabilityChecker
{
    private static readonly IReadOnlyList<long> EmptyOperationIds = new List<long>().AsReadOnly();

    public static LinearizabilityResult Check<TState, TInput, TOutput>(
        LinearizabilityHistory<TInput, TOutput> history,
        LinearizabilitySpecification<TState, TInput, TOutput> specification,
        LinearizabilityOptions options)
        where TState : notnull
    {
        ValidateOptions(options);
        var operations = history.CompletedOperations;

        if (operations.Count > options.MaxOperations || operations.Count > 63)
        {
            return new LinearizabilityResult(
                LinearizabilityStatus.Inconclusive,
                new LinearizabilitySearchMetrics(operations.Count, history.PendingOperations, 0),
                EmptyOperationIds,
                EmptyOperationIds,
                $"History contains {operations.Count} completed operations, exceeding the configured bounded limit of " +
                $"{Math.Min(options.MaxOperations, 63)}.");
        }

        if (operations.Count == 0)
        {
            return new LinearizabilityResult(
                LinearizabilityStatus.Linearizable,
                new LinearizabilitySearchMetrics(0, history.PendingOperations, 1),
                EmptyOperationIds,
                EmptyOperationIds,
                null);
        }

        var predecessors = BuildPredecessors(operations);
        var search = new LinearizabilitySearch<TState, TInput, TOutput>(
            operations,
            predecessors,
            specification,
            options.MaxSearchStates);
        return search.Run(history.PendingOperations);
    }

    private static IReadOnlyList<ulong> BuildPredecessors<TInput, TOutput>(
        IReadOnlyList<LinearizabilityOperation<TInput, TOutput>> operations)
    {
        var predecessors = new List<ulong>(operations.Count);

        for (var current = 0; current < operations.Count; current++)
        {
            ulong mask = 0;

            for (var candidate = 0; candidate < operations.Count; candidate++)
            {
                if (candidate != current && operations[candidate].ReturnSequence < operations[current].CallSequence)
                {
                    mask |= 1UL << candidate;
                }
            }

            predecessors.Add(mask);
        }

        return predecessors.AsReadOnly();
    }

    private static void ValidateOptions(LinearizabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxSearchStates <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxSearchStates, "MaxSearchStates must be greater than zero.");
        }

        if (options.MaxOperations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxOperations, "MaxOperations must be greater than zero.");
        }
    }
}
