namespace Causalia.Dapr;

internal sealed class InternalDaprSubscription
{
    public required long Id { get; init; }

    public required string SubscriberName { get; init; }

    public required Type PayloadType { get; init; }

    public required DaprSubscriptionOptions Options { get; init; }

    public required Func<long, object, int, CancellationToken, Task<DaprPubSubResult>> Handler { get; init; }
}
