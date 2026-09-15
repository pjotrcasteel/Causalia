namespace Causalia.RabbitMQ;

/// <summary>
/// Provides RabbitMQ simulation factories for a simulation context.
/// </summary>
public static class SimulationContextRabbitMqExtensions
{
    /// <summary>
    /// Creates a deterministic RabbitMQ broker.
    /// </summary>
    public static SimulationRabbitBroker CreateRabbitMqBroker(
        this SimulationContext context,
        string name = "rabbitmq",
        RabbitFaultPlan? faults = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimulationRabbitBroker(context, name, faults);
    }
}
