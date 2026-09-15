namespace Causalia.Linearizability;

/// <summary>
/// Represents an in-flight history operation that can be completed with its observed output.
/// </summary>
public sealed class LinearizabilityOperationHandle<TInput, TOutput>
{
    private readonly LinearizabilityHistory<TInput, TOutput> _history;
    private bool _completed;

    internal LinearizabilityOperationHandle(
        LinearizabilityHistory<TInput, TOutput> history,
        long id,
        string clientId,
        TInput input)
    {
        _history = history;
        Id = id;
        ClientId = clientId;
        Input = input;
    }

    /// <summary>
    /// Gets the stable operation id within the history.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// Gets the logical client that invoked the operation.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets the operation input.
    /// </summary>
    public TInput Input { get; }

    /// <summary>
    /// Records the observed return value and closes the operation.
    /// </summary>
    public void Complete(TOutput output)
    {
        if (_completed)
        {
            throw new InvalidOperationException($"Linearizability operation {Id} has already completed.");
        }

        _history.Complete(Id, output);
        _completed = true;
    }
}
