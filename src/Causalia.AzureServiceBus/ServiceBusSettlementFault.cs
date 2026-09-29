namespace Causalia.AzureServiceBus;

/// <summary>Observable failure at a settlement boundary.</summary>
public enum ServiceBusSettlementFault
{
    /// <summary>A settlement outcome or action.</summary>
    None,
    /// <summary>A settlement outcome or action.</summary>
    Reject,
    /// <summary>A settlement outcome or action.</summary>
    LoseAcknowledgement
}
