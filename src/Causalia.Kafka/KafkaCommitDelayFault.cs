namespace Causalia.Kafka;

/// <summary>
/// Adds virtual latency to one Kafka offset commit.
/// </summary>
public sealed record KafkaCommitDelayFault(TimeSpan Delay) : KafkaCommitFault;
