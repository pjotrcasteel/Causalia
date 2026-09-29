namespace Causalia.AzureServiceBus;

/// <summary>A snapshot of one PeekLock delivery.</summary>
public sealed record ServiceBusDelivery(
    long SequenceNumber,
    ReadOnlyMemory<byte> Body,
    int DeliveryCount,
    Guid LockToken,
    DateTimeOffset LockedUntil);
