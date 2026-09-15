using Causalia.Faults;

namespace Causalia.RabbitMQ;

/// <summary>
/// Configures deterministic RabbitMQ publisher-side faults.
/// </summary>
public sealed class RabbitFaultPlan
{
    internal FaultPlan<RabbitPublishFaultContext, RabbitPublishFault> Plan { get; } = new();

    /// <summary>
    /// Rejects publishes before broker acceptance using the specified probability.
    /// </summary>
    public RabbitFaultPlan RejectPublish(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<RabbitPublishFaultContext, RabbitPublishFault>(
            "rabbitmq.publish.rejected",
            probability,
            _ => new RabbitPublishRejectedFault()));
        return this;
    }

    /// <summary>
    /// Loses publisher confirms after routing using the specified probability.
    /// </summary>
    public RabbitFaultPlan LosePublisherConfirm(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<RabbitPublishFaultContext, RabbitPublishFault>(
            "rabbitmq.publish.confirm-lost",
            probability,
            _ => new RabbitPublisherConfirmLostFault()));
        return this;
    }

    /// <summary>
    /// Adds virtual publish latency using the specified probability.
    /// </summary>
    public RabbitFaultPlan DelayPublish(double probability, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        Plan.Add(new ProbabilityFaultPolicy<RabbitPublishFaultContext, RabbitPublishFault>(
            "rabbitmq.publish.delay",
            probability,
            _ => new RabbitPublishDelayFault(delay)));
        return this;
    }
}
