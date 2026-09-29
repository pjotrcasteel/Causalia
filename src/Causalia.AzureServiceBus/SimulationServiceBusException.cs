namespace Causalia.AzureServiceBus;

/// <summary>Indicates whether the broker committed a settlement despite a client-visible failure.</summary>
public sealed class SimulationServiceBusException : Exception
{
    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public SimulationServiceBusException(string message, bool settlementCommitted) : base(message)
    {
        SettlementCommitted = settlementCommitted;
    }

    /// <summary>Performs the indicated deterministic Service Bus operation.</summary>
    public bool SettlementCommitted { get; }
}
