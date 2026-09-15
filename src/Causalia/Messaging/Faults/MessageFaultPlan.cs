using Causalia.Faults;

namespace Causalia.Messaging.Faults;

/// <summary>
/// Provides fluent messaging-specific policies backed by the generic Causalia fault engine.
/// </summary>
public sealed class MessageFaultPlan
{
    internal FaultPlan<MessageFaultContext, MessageFault> Plan { get; } = new();

    /// <summary>
    /// Drops messages with the specified deterministic probability.
    /// </summary>
    public MessageFaultPlan Drop(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<MessageFaultContext, MessageFault>("messaging.drop", probability, _ => new DropMessageFault()));
        return this;
    }

    /// <summary>
    /// Adds duplicate deliveries with the specified deterministic probability.
    /// </summary>
    public MessageFaultPlan Duplicate(double probability, int additionalCopies = 1)
    {
        if (additionalCopies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalCopies), additionalCopies, "AdditionalCopies must be greater than zero.");
        }

        Plan.Add(
            new ProbabilityFaultPolicy<MessageFaultContext, MessageFault>(
                "messaging.duplicate",
                probability,
                _ => new DuplicateMessageFault(additionalCopies)));
        return this;
    }

    /// <summary>
    /// Adds virtual latency with the specified deterministic probability.
    /// </summary>
    public MessageFaultPlan Delay(double probability, TimeSpan additionalDelay)
    {
        if (additionalDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalDelay), additionalDelay, "AdditionalDelay cannot be negative.");
        }

        Plan.Add(
            new ProbabilityFaultPolicy<MessageFaultContext, MessageFault>(
                "messaging.delay",
                probability,
                _ => new DelayMessageFault(additionalDelay)));
        return this;
    }

    /// <summary>
    /// Allows messages to overtake pending messages with the specified deterministic probability.
    /// </summary>
    public MessageFaultPlan Reorder(double probability)
    {
        Plan.Add(new ProbabilityFaultPolicy<MessageFaultContext, MessageFault>("messaging.reorder", probability, _ => new ReorderMessageFault()));
        return this;
    }
}
