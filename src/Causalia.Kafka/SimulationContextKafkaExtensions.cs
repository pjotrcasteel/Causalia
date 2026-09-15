namespace Causalia.Kafka;

/// <summary>
/// Provides Kafka simulation factories for a simulation context.
/// </summary>
public static class SimulationContextKafkaExtensions
{
    /// <summary>
    /// Creates a deterministic in-memory Kafka cluster.
    /// </summary>
    public static SimulationKafkaCluster CreateKafkaCluster(
        this SimulationContext context,
        string name = "kafka",
        KafkaFaultPlan? faults = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimulationKafkaCluster(context, name, faults);
    }
}
