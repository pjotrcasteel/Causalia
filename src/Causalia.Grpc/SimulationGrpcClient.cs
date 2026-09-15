namespace Causalia.Grpc;

/// <summary>
/// Represents one deterministic logical gRPC client route.
/// </summary>
public sealed class SimulationGrpcClient
{
    private readonly SimulationGrpcNetwork _network;

    internal SimulationGrpcClient(SimulationGrpcNetwork network, string source, string destination)
    {
        _network = network;
        Source = source;
        Destination = destination;
    }

    /// <summary>Gets the logical source service.</summary>
    public string Source { get; }

    /// <summary>Gets the logical destination service.</summary>
    public string Destination { get; }

    /// <summary>
    /// Executes one typed unary RPC using deterministic deadline and retry behavior.
    /// </summary>
    public Task<TResponse> UnaryAsync<TRequest, TResponse>(
        string service,
        string method,
        TRequest request,
        SimulationGrpcCallOptions? options,
        CancellationToken cancellationToken)
    {
        return _network.UnaryAsync<TRequest, TResponse>(
            new GrpcUnaryCall<TRequest>
            {
                Source = Source,
                Destination = Destination,
                Service = service,
                Method = method,
                Request = request
            },
            options ?? new SimulationGrpcCallOptions(),
            cancellationToken);
    }
}
