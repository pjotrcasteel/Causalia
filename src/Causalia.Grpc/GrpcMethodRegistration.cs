namespace Causalia.Grpc;

internal sealed class GrpcMethodRegistration
{
    public required Type RequestType { get; init; }

    public required Type ResponseType { get; init; }

    public required Func<object, CancellationToken, Task<object?>> Handler { get; init; }
}
