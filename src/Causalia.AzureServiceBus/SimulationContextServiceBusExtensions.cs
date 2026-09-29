using Causalia.Faults;

namespace Causalia.AzureServiceBus;

/// <summary>Creates a broker scoped to the current deterministic simulation.</summary>
public static class SimulationContextServiceBusExtensions
{
    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public static SimulationServiceBus CreateAzureServiceBus(
        this SimulationContext context,
        FaultPlan<ServiceBusSettlementEvent, ServiceBusSettlementFault>? faults = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimulationServiceBus(context, faults);
    }
}
