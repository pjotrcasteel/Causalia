namespace Causalia.Grpc;

/// <summary>
/// Describes one deterministic gRPC transport attempt for fault evaluation.
/// </summary>
public sealed record GrpcCallContext
{
    /// <summary>Gets the logical call identifier.</summary>
    public required long CallId { get; init; }
    /// <summary>Gets the source service name.</summary>
    public required string Source { get; init; }
    /// <summary>Gets the destination service name.</summary>
    public required string Destination { get; init; }
    /// <summary>Gets the gRPC service name.</summary>
    public required string Service { get; init; }
    /// <summary>Gets the gRPC method name.</summary>
    public required string Method { get; init; }
    /// <summary>Gets the one-based attempt number.</summary>
    public required int Attempt { get; init; }
}
