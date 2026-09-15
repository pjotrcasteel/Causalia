namespace Causalia.Dapr;

/// <summary>
/// Loses a successful subscriber acknowledgement so the event is redelivered.
/// </summary>
public sealed record DaprAcknowledgementLostFault : DaprDeliveryFault;
