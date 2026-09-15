namespace Causalia.Messaging.Exceptions;

/// <summary>
/// Indicates that an endpoint already has a handler for the same message type.
/// </summary>
public sealed class DuplicateMessageHandlerException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateMessageHandlerException"/> class.
    /// </summary>
    public DuplicateMessageHandlerException(string endpoint, Type messageType)
        : base($"Endpoint '{endpoint}' already has a handler for message type '{messageType.FullName ?? messageType.Name}'.")
    {
    }
}
