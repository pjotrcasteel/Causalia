namespace Causalia.Messaging.Faults;

/// <summary>
/// Allows the logical message to overtake already pending messages on the same endpoint.
/// </summary>
public sealed record ReorderMessageFault : MessageFault;
