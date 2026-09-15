namespace Causalia.Linearizability;

internal sealed record PendingLinearizabilityOperation<TInput>(
    string ClientId,
    TInput Input,
    long CallSequence,
    DateTimeOffset CallTime);
