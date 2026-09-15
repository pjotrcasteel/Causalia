namespace Causalia.Linearizability;

/// <summary>
/// Represents one completed operation in a concurrent history.
/// </summary>
public sealed class LinearizabilityOperation<TInput, TOutput>
{
    internal LinearizabilityOperation(
        long id,
        string clientId,
        TInput input,
        TOutput output,
        LinearizabilityOperationBoundary call,
        LinearizabilityOperationBoundary completion)
    {
        Id = id;
        ClientId = clientId;
        Input = input;
        Output = output;
        CallSequence = call.Sequence;
        CallTime = call.Time;
        ReturnSequence = completion.Sequence;
        ReturnTime = completion.Time;
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
    /// Gets the deterministic invocation sequence.
    /// </summary>
    public long CallSequence { get; }

    /// <summary>
    /// Gets the virtual invocation time.
    /// </summary>
    public DateTimeOffset CallTime { get; }

    /// <summary>
    /// Gets the observed operation output.
    /// </summary>
    public TOutput Output { get; }

    /// <summary>
    /// Gets the deterministic completion sequence.
    /// </summary>
    public long ReturnSequence { get; }

    /// <summary>
    /// Gets the virtual completion time.
    /// </summary>
    public DateTimeOffset ReturnTime { get; }
}
