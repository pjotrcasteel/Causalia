namespace Causalia.Grpc;

/// <summary>
/// Adds virtual transport latency to one gRPC attempt.
/// </summary>
public sealed record DelayGrpcFault(TimeSpan Delay) : GrpcNetworkFault;
