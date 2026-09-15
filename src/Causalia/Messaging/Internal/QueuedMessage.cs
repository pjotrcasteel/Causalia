namespace Causalia.Messaging.Internal;

internal sealed class QueuedMessage
{
    public required long MessageId { get; init; }

    public required int Attempt { get; set; }

    public required string Endpoint { get; init; }

    public required object Message { get; init; }

    public required Type MessageType { get; init; }

    public required DateTimeOffset EnqueuedAt { get; init; }

    public required DateTimeOffset DueAt { get; init; }

    public required MessageHandlerRegistration Registration { get; init; }

    public required CancellationToken CancellationToken { get; init; }

    public TaskCompletionSource<bool>? Completion { get; init; }
}
