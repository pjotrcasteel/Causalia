namespace Causalia.Kafka;

/// <summary>
/// Applies a Kafka commit but loses its acknowledgement to the caller.
/// </summary>
public sealed record KafkaCommitAcknowledgementLostFault : KafkaCommitFault;
