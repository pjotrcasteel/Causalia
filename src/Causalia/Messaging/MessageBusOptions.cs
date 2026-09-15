using Causalia.Messaging.Faults;

namespace Causalia.Messaging;

/// <summary>
/// Configures deterministic message delivery for a simulated message bus.
/// </summary>
public sealed class MessageBusOptions
{
    /// <summary>
    /// Gets or initializes the fixed virtual latency applied to every message.
    /// </summary>
    public TimeSpan DeliveryLatency { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or initializes the stable fault-randomness scope for this bus.
    /// </summary>
    public string FaultScope { get; init; } = "messaging";

    /// <summary>
    /// Gets or initializes optional deterministic message fault policies.
    /// </summary>
    public MessageFaultPlan? Faults { get; init; }
}
