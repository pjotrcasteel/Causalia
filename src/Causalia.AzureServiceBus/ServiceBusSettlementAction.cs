namespace Causalia.AzureServiceBus;

/// <summary>Requested settlement operation.</summary>
public enum ServiceBusSettlementAction
{
    /// <summary>A settlement outcome or action.</summary>
    Complete,
    /// <summary>A settlement outcome or action.</summary>
    Abandon,
    /// <summary>A settlement outcome or action.</summary>
    DeadLetter
}
