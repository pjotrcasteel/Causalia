namespace Causalia.Messaging.Internal;

internal sealed class LogicalMessage
{
    public required long MessageId { get; init; }

    public required string Endpoint { get; init; }

    public required object Message { get; init; }

    public required Type MessageType { get; init; }

    public required MessageHandlerRegistration Registration { get; init; }

    public required DateTimeOffset EnqueuedAt { get; init; }

    public required bool WaitForCompletion { get; init; }

    public required CancellationToken CancellationToken { get; init; }
}
