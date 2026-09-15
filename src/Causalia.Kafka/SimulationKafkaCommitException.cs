namespace Causalia.Kafka;

/// <summary>
/// Represents a deterministic Kafka commit failure, including whether the broker state changed.
/// </summary>
public sealed class SimulationKafkaCommitException : Exception
{
    /// <summary>
    /// Initializes a Kafka commit exception.
    /// </summary>
    public SimulationKafkaCommitException(string message, bool wasCommitted)
        : base(message)
    {
        WasCommitted = wasCommitted;
    }

    /// <summary>
    /// Gets whether the offset was durably committed despite the caller observing a failure.
    /// </summary>
    public bool WasCommitted { get; }
}
