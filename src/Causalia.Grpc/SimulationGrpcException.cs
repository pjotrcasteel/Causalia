namespace Causalia.Grpc;

/// <summary>
/// Represents a deterministic gRPC call failure with a canonical status code.
/// </summary>
public sealed class SimulationGrpcException : Exception
{
    /// <summary>
    /// Initializes a gRPC simulation exception.
    /// </summary>
    public SimulationGrpcException(SimulationGrpcStatusCode statusCode, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the canonical gRPC status code.
    /// </summary>
    public SimulationGrpcStatusCode StatusCode { get; }
}
