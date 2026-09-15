namespace Causalia.Grpc;

internal sealed record GrpcUnaryCall<TRequest>
{
    public required string Source { get; init; }

    public required string Destination { get; init; }

    public required string Service { get; init; }

    public required string Method { get; init; }

    public required TRequest Request { get; init; }
}
