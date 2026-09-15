namespace Causalia.Messaging.Internal;

internal readonly record struct MessageHandlerKey(string Endpoint, Type MessageType);
