using Causalia.Nodes;

namespace Causalia.Grpc;

/// <summary>
/// Hosts deterministic unary gRPC method handlers for one logical service endpoint.
/// </summary>
public sealed class SimulationGrpcServer
{
    private readonly Dictionary<(string Service, string Method), GrpcMethodRegistration> _methods = new();

    internal SimulationGrpcServer(string name, SimulationNode? node)
    {
        Name = name;
        Node = node;
    }

    /// <summary>
    /// Gets the logical server name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional lifecycle node controlling server availability.
    /// </summary>
    public SimulationNode? Node { get; }

    /// <summary>
    /// Gets whether the server is currently available.
    /// </summary>
    public bool IsAvailable => Node?.IsRunning ?? true;

    /// <summary>
    /// Registers one typed unary method.
    /// </summary>
    public void RegisterUnary<TRequest, TResponse>(
        string service,
        string method,
        Func<TRequest, CancellationToken, Task<TResponse>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(handler);
        var key = (service, method);

        if (_methods.ContainsKey(key))
        {
            throw new InvalidOperationException($"gRPC method '{service}/{method}' is already registered.");
        }

        _methods.Add(key, new GrpcMethodRegistration
        {
            RequestType = typeof(TRequest),
            ResponseType = typeof(TResponse),
            Handler = async (request, cancellationToken) => await handler((TRequest)request, cancellationToken)
        });
    }

    internal async Task<object?> InvokeAsync(
        string service,
        string method,
        object request,
        Type responseType,
        CancellationToken cancellationToken)
    {
        if (!_methods.TryGetValue((service, method), out var registration))
        {
            throw new SimulationGrpcException(SimulationGrpcStatusCode.Unimplemented, $"gRPC method '{service}/{method}' is not registered.");
        }

        if (!registration.RequestType.IsInstanceOfType(request) || registration.ResponseType != responseType)
        {
            throw new SimulationGrpcException(SimulationGrpcStatusCode.Internal, $"gRPC method '{service}/{method}' was called with incompatible types.");
        }

        if (Node is null)
        {
            return await registration.Handler(request, cancellationToken);
        }

        object? result = null;
        await Node.RunAsync(
            async nodeToken => result = await registration.Handler(request, nodeToken),
            cancellationToken);
        return result;
    }
}
