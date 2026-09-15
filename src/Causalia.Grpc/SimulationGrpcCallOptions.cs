namespace Causalia.Grpc;

/// <summary>
/// Configures deterministic deadline and retry behavior for one logical gRPC call.
/// </summary>
public sealed class SimulationGrpcCallOptions
{
    /// <summary>
    /// Gets or initializes a virtual timeout for the entire logical call.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of attempts including the first attempt.
    /// </summary>
    public int MaxAttempts { get; init; } = 1;

    /// <summary>
    /// Gets or initializes the virtual delay between retry attempts.
    /// </summary>
    public TimeSpan RetryBackoff { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or initializes the statuses that may be retried.
    /// </summary>
    public IReadOnlySet<SimulationGrpcStatusCode> RetryableStatusCodes { get; init; } =
        new HashSet<SimulationGrpcStatusCode> { SimulationGrpcStatusCode.Unavailable };
}
