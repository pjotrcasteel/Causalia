using Causalia.Faults;

namespace Causalia.Dapr;

/// <summary>
/// Configures deterministic Dapr pub/sub delivery faults.
/// </summary>
public sealed class DaprPubSubFaultPlan
{
    internal FaultPlan<DaprDeliveryFaultContext, DaprDeliveryFault> Plan { get; } = new();

    /// <summary>
    /// Adds virtual delivery latency with the specified deterministic probability.
    /// </summary>
    public DaprPubSubFaultPlan Delay(double probability, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        Plan.Add(new ProbabilityFaultPolicy<DaprDeliveryFaultContext, DaprDeliveryFault>(
            "dapr.pubsub.delay",
            probability,
            _ => new DaprDeliveryDelayFault(delay)));
        return this;
    }

    /// <summary>
    /// Loses successful acknowledgements with the specified deterministic probability, causing at-least-once redelivery.
    /// </summary>
    public DaprPubSubFaultPlan LoseAcknowledgement(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<DaprDeliveryFaultContext, DaprDeliveryFault>(
            "dapr.pubsub.ack-lost",
            probability,
            _ => new DaprAcknowledgementLostFault()));
        return this;
    }
}
