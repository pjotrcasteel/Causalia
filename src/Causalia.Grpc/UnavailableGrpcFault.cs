namespace Causalia.Grpc;

/// <summary>
/// Makes one gRPC attempt fail with UNAVAILABLE.
/// </summary>
public sealed record UnavailableGrpcFault : GrpcNetworkFault;
