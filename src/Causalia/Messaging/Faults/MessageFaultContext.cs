namespace Causalia.Messaging.Faults;

/// <summary>
/// Describes a logical message send before fault effects are applied.
/// </summary>
public sealed record MessageFaultContext(long MessageId, string Endpoint, Type MessageType, DateTimeOffset EnqueuedAt);
