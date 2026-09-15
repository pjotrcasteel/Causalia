namespace Causalia.Messaging.Faults;

/// <summary>
/// Drops the logical message before it reaches the endpoint.
/// </summary>
public sealed record DropMessageFault : MessageFault;
