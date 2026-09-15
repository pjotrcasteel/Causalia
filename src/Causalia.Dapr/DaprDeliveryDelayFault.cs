namespace Causalia.Dapr;

/// <summary>
/// Adds virtual latency before one Dapr delivery attempt.
/// </summary>
public sealed record DaprDeliveryDelayFault(TimeSpan Delay) : DaprDeliveryFault;
