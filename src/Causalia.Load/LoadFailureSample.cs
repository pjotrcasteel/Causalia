namespace Causalia.Load;

/// <summary>
/// Describes one recorded iteration failure without retaining an exception object graph.
/// </summary>
public sealed class LoadFailureSample
{
    internal LoadFailureSample(long iterationId, int actorId, string exceptionType, string message)
    {
        IterationId = iterationId;
        ActorId = actorId;
        ExceptionType = exceptionType;
        Message = message;
    }

    /// <summary>
    /// Gets the logical iteration identifier that failed.
    /// </summary>
    public long IterationId { get; }

    /// <summary>
    /// Gets the logical actor identifier that executed the failed iteration.
    /// </summary>
    public int ActorId { get; }

    /// <summary>
    /// Gets the exception type name.
    /// </summary>
    public string ExceptionType { get; }

    /// <summary>
    /// Gets the exception message.
    /// </summary>
    public string Message { get; }
}
