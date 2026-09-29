namespace Causalia.AzureServiceBus;

/// <summary>Typed event passed to the existing deterministic fault injector.</summary>
public sealed record ServiceBusSettlementEvent(string Entity, long SequenceNumber, ServiceBusSettlementAction Action);
