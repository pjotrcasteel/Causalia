namespace Causalia.RabbitMQ;

/// <summary>
/// Represents a deterministic publisher failure, including whether queues received the message.
/// </summary>
public sealed class SimulationRabbitPublishException : Exception
{
    /// <summary>
    /// Initializes a RabbitMQ publish exception.
    /// </summary>
    public SimulationRabbitPublishException(string message, bool wasRouted)
        : base(message)
    {
        WasRouted = wasRouted;
    }

    /// <summary>
    /// Gets whether the broker routed the publish despite the caller observing a failure.
    /// </summary>
    public bool WasRouted { get; }
}
