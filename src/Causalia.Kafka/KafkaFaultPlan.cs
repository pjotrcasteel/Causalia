using Causalia.Faults;

namespace Causalia.Kafka;

/// <summary>
/// Configures deterministic Kafka offset-commit faults.
/// </summary>
public sealed class KafkaFaultPlan
{
    internal FaultPlan<KafkaCommitFaultContext, KafkaCommitFault> Plan { get; } = new();

    /// <summary>
    /// Fails commits before they reach the coordinator using the specified probability.
    /// </summary>
    public KafkaFaultPlan RejectCommit(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<KafkaCommitFaultContext, KafkaCommitFault>(
            "kafka.commit.rejected",
            probability,
            _ => new KafkaCommitRejectedFault()));
        return this;
    }

    /// <summary>
    /// Applies commits but loses their acknowledgements using the specified probability.
    /// </summary>
    public KafkaFaultPlan LoseCommitAcknowledgement(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<KafkaCommitFaultContext, KafkaCommitFault>(
            "kafka.commit.ack-lost",
            probability,
            _ => new KafkaCommitAcknowledgementLostFault()));
        return this;
    }

    /// <summary>
    /// Adds virtual commit latency using the specified probability.
    /// </summary>
    public KafkaFaultPlan DelayCommit(double probability, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        Plan.Add(new ProbabilityFaultPolicy<KafkaCommitFaultContext, KafkaCommitFault>(
            "kafka.commit.delay",
            probability,
            _ => new KafkaCommitDelayFault(delay)));
        return this;
    }
}
