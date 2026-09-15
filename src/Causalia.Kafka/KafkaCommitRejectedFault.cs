namespace Causalia.Kafka;

/// <summary>
/// Fails a Kafka commit before the committed offset changes.
/// </summary>
public sealed record KafkaCommitRejectedFault : KafkaCommitFault;
