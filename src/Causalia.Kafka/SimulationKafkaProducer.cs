using System.Text.Json;

namespace Causalia.Kafka;

/// <summary>
/// Produces deterministic records into a simulated Kafka cluster.
/// </summary>
public sealed class SimulationKafkaProducer
{
    private readonly SimulationKafkaCluster _cluster;

    internal SimulationKafkaProducer(SimulationKafkaCluster cluster)
    {
        _cluster = cluster;
    }

    /// <summary>
    /// Produces one typed record, using a stable key hash or deterministic round-robin partitioning.
    /// </summary>
    public Task<KafkaProduceResult> ProduceAsync<T>(
        string topic,
        string? key,
        T value,
        CancellationToken cancellationToken)
    {
        return _cluster.ProduceAsync(topic, key, JsonSerializer.SerializeToUtf8Bytes(value), cancellationToken);
    }
}
