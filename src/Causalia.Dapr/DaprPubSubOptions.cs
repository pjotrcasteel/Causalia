namespace Causalia.Dapr;

/// <summary>
/// Configures one deterministic Dapr pub/sub component.
/// </summary>
public sealed class DaprPubSubOptions
{
    /// <summary>
    /// Gets or initializes deterministic delivery and acknowledgement fault policies.
    /// </summary>
    public DaprPubSubFaultPlan? Faults { get; init; }
}
