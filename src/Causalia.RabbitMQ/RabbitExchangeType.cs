namespace Causalia.RabbitMQ;

/// <summary>
/// Identifies a simulated RabbitMQ exchange routing mode.
/// </summary>
public enum RabbitExchangeType
{
    /// <summary>
    /// Routes messages by exact routing-key match.
    /// </summary>
    Direct,

    /// <summary>
    /// Routes every published message to every bound queue.
    /// </summary>
    Fanout
}
