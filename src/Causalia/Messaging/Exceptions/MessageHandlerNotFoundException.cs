namespace Causalia.Messaging.Exceptions;

/// <summary>
/// Indicates that no handler is registered for a message destination and type.
/// </summary>
public sealed class MessageHandlerNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHandlerNotFoundException"/> class.
    /// </summary>
    public MessageHandlerNotFoundException(string endpoint, Type messageType)
        : base($"Endpoint '{endpoint}' has no handler for message type '{messageType.FullName ?? messageType.Name}'.")
    {
    }
}
