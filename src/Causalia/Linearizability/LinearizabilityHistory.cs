namespace Causalia.Linearizability;

/// <summary>
/// Records deterministic call and return boundaries for concurrent operations.
/// </summary>
public sealed class LinearizabilityHistory<TInput, TOutput>
{
    private readonly Dictionary<long, PendingLinearizabilityOperation<TInput>> _pending = new();
    private readonly List<LinearizabilityOperation<TInput, TOutput>> _completed = new();
    private readonly SimulationContext _context;
    private long _nextOperationId;
    private long _nextSequence;

    internal LinearizabilityHistory(SimulationContext context, string name)
    {
        _context = context;
        Name = name;
    }

    /// <summary>
    /// Gets the logical history name used in diagnostics.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a snapshot of completed operations ordered by operation id.
    /// </summary>
    public IReadOnlyList<LinearizabilityOperation<TInput, TOutput>> CompletedOperations => _completed
        .OrderBy(operation => operation.Id)
        .ToList()
        .AsReadOnly();

    /// <summary>
    /// Gets the number of operations that have been invoked but not completed.
    /// </summary>
    public int PendingOperations => _pending.Count;

    /// <summary>
    /// Begins an operation and records its deterministic invocation boundary.
    /// </summary>
    public LinearizabilityOperationHandle<TInput, TOutput> Begin(string clientId, TInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        var id = checked(++_nextOperationId);
        var callSequence = checked(++_nextSequence);
        var operation = new PendingLinearizabilityOperation<TInput>(clientId, input, callSequence, _context.TimeProvider.GetUtcNow());
        _pending.Add(id, operation);
        _context.TraceEvent($"linearizability:{Name}:call:{id}:client:{clientId}");
        return new LinearizabilityOperationHandle<TInput, TOutput>(this, id, clientId, input);
    }

    /// <summary>
    /// Checks the completed portion of this history against an executable sequential specification.
    /// </summary>
    public LinearizabilityResult Check<TState>(
        LinearizabilitySpecification<TState, TInput, TOutput> specification,
        LinearizabilityOptions? options = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(specification);
        return LinearizabilityChecker.Check(this, specification, options ?? new LinearizabilityOptions());
    }

    /// <summary>
    /// Requires the completed portion of this history to be provably linearizable within the configured bounds.
    /// </summary>
    public LinearizabilityResult RequireLinearizable<TState>(
        LinearizabilitySpecification<TState, TInput, TOutput> specification,
        LinearizabilityOptions? options = null)
        where TState : notnull
    {
        var result = Check(specification, options);

        if (result.Status == LinearizabilityStatus.NotLinearizable)
        {
            throw new SimulationLinearizabilityViolationException(Name, result);
        }

        if (result.Status == LinearizabilityStatus.Inconclusive)
        {
            throw new SimulationLinearizabilityInconclusiveException(Name, result);
        }

        return result;
    }

    internal void Complete(long id, TOutput output)
    {
        if (!_pending.Remove(id, out var pending))
        {
            throw new InvalidOperationException($"Linearizability operation {id} is not pending in history '{Name}'.");
        }

        var returnSequence = checked(++_nextSequence);
        _completed.Add(
            new LinearizabilityOperation<TInput, TOutput>(
                id,
                pending.ClientId,
                pending.Input,
                output,
                new LinearizabilityOperationBoundary(pending.CallSequence, pending.CallTime),
                new LinearizabilityOperationBoundary(returnSequence, _context.TimeProvider.GetUtcNow())));
        _context.TraceEvent($"linearizability:{Name}:return:{id}:client:{pending.ClientId}");
    }

}
