namespace Causalia.Visualization;

/// <summary>
/// Classifies one event in a visualized Causalia trace.
/// </summary>
public enum TraceEventCategory
{
    /// <summary>
    /// User-defined or otherwise unclassified trace event.
    /// </summary>
    User,

    /// <summary>
    /// Deterministic scheduler activity.
    /// </summary>
    Scheduler,

    /// <summary>
    /// Simulated node lifecycle activity.
    /// </summary>
    Node,

    /// <summary>
    /// Restartable process and generation lifecycle activity.
    /// </summary>
    Process,

    /// <summary>
    /// Deterministic messaging activity.
    /// </summary>
    Messaging,

    /// <summary>
    /// ASP.NET Core host and request activity.
    /// </summary>
    AspNetCore,

    /// <summary>
    /// Service-to-service HTTP network activity.
    /// </summary>
    Network,

    /// <summary>
    /// Durable storage activity.
    /// </summary>
    Storage,

    /// <summary>
    /// Fault injection activity.
    /// </summary>
    Fault,

    /// <summary>
    /// Invariant registration, satisfaction or failure.
    /// </summary>
    Invariant,

    /// <summary>
    /// Linearizability history activity.
    /// </summary>
    Linearizability,

    /// <summary>
    /// Deterministic simulated-load activity.
    /// </summary>
    Load,
    /// <summary>
    /// Dapr building-block activity.
    /// </summary>
    Dapr,

    /// <summary>
    /// Kafka topic, consumer-group and offset activity.
    /// </summary>
    Kafka,

    /// <summary>
    /// RabbitMQ exchange, queue and acknowledgement activity.
    /// </summary>
    RabbitMQ,

    /// <summary>
    /// gRPC call, deadline and retry activity.
    /// </summary>
    Grpc,

    /// <summary>
    /// Distributed-consistency history and convergence activity.
    /// </summary>
    Consistency,

    /// <summary>
    /// Executable model command and verification activity.
    /// </summary>
    ModelBased,

    /// <summary>
    /// Production-derived timing, sampling and incident replay activity.
    /// </summary>
    ProductionReality,
}
