using Causalia.Nodes;

namespace Causalia.Grpc;

/// <summary>
/// Simulates deterministic gRPC unary calls, partitions, deadlines, status failures and retries.
/// </summary>
public sealed class SimulationGrpcNetwork
{
    private readonly SimulationContext _context;
    private readonly Dictionary<(string Source, string Destination), SimulationGrpcNetworkLink> _links = new();
    private readonly Dictionary<string, SimulationGrpcServer> _servers = new(StringComparer.Ordinal);
    private readonly GrpcFaultPlan? _defaultFaults;
    private long _nextCallId;

    internal SimulationGrpcNetwork(SimulationContext context, GrpcFaultPlan? defaultFaults)
    {
        _context = context;
        _defaultFaults = defaultFaults;
        _context.TraceEvent("grpc:network:created");
    }

    /// <summary>
    /// Creates a server that may optionally be bound to a simulated node lifecycle.
    /// </summary>
    public SimulationGrpcServer CreateServer(string name, SimulationNode? node = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (node is not null && !node.BelongsTo(_context))
        {
            throw new ArgumentException("The gRPC server node belongs to a different simulation context.", nameof(node));
        }

        return new SimulationGrpcServer(_context, name, node);
    }

    /// <summary>
    /// Registers a server under one logical destination name.
    /// </summary>
    public void Register(string serviceName, SimulationGrpcServer server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(server);

        if (!server.BelongsTo(_context))
        {
            throw new ArgumentException("The gRPC server belongs to a different simulation context.", nameof(server));
        }

        if (!_servers.TryAdd(serviceName, server))
        {
            throw new InvalidOperationException($"A gRPC server named '{serviceName}' is already registered.");
        }

        _context.TraceEvent($"grpc:server:registered:{serviceName}");
    }

    /// <summary>
    /// Gets or creates one directed gRPC network link.
    /// </summary>
    public SimulationGrpcNetworkLink Between(string source, string destination)
    {
        var key = (source, destination);

        if (_links.TryGetValue(key, out var link))
        {
            return link;
        }

        link = new SimulationGrpcNetworkLink(_context, source, destination, _defaultFaults);
        _links.Add(key, link);
        return link;
    }

    /// <summary>
    /// Creates a logical gRPC client from one service to one registered destination.
    /// </summary>
    public SimulationGrpcClient CreateClient(string source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        _ = Resolve(destination);
        return new SimulationGrpcClient(this, source, destination);
    }

    internal async Task<TResponse> UnaryAsync<TRequest, TResponse>(
        GrpcUnaryCall<TRequest> call,
        SimulationGrpcCallOptions options,
        CancellationToken cancellationToken)
    {
        Validate(options);
        cancellationToken.ThrowIfCancellationRequested();
        var callId = checked(++_nextCallId);
        _context.TraceEvent(
            $"grpc:call:started:{callId}:{call.Source}->{call.Destination}:{call.Service}/{call.Method}");
        using var deadline = CreateDeadline(options.Timeout, cancellationToken);
        var callToken = deadline?.Token ?? cancellationToken;

        try
        {
            for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
            {
                callToken.ThrowIfCancellationRequested();

                try
                {
                    var operation = InvokeAttemptAsync<TRequest, TResponse>(callId, attempt, call, callToken);
                    var result = await AwaitResponseAsync(operation, callId, callToken);
                    _context.TraceEvent($"grpc:call:completed:{callId}:attempt:{attempt}:status:Ok");
                    return result;
                }
                catch (SimulationGrpcException exception) when (CanRetry(exception, attempt, options))
                {
                    _context.TraceEvent(
                        $"grpc:call:retry:{callId}:attempt:{attempt + 1}:status:{exception.StatusCode}");

                    if (options.RetryBackoff > TimeSpan.Zero)
                    {
                        await Task.Delay(options.RetryBackoff, _context.TimeProvider, callToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline?.IsDeadlineExceeded == true)
        {
            _context.TraceEvent($"grpc:call:deadline:{callId}");
            throw new SimulationGrpcException(
                SimulationGrpcStatusCode.DeadlineExceeded,
                $"gRPC call '{call.Service}/{call.Method}' exceeded its virtual deadline.");
        }

        throw new SimulationGrpcException(SimulationGrpcStatusCode.Unknown, "gRPC retry loop exhausted unexpectedly.");
    }

    private async Task<TResponse> AwaitResponseAsync<TResponse>(Task<TResponse> operation, long callId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await operation.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client stops waiting independently of the server. Keep late server effects under simulation control.
            _context.TrackOperation(ObserveAbandonedCallAsync(operation, callId));
            throw;
        }
    }

    private async Task ObserveAbandonedCallAsync(Task operation, long callId)
    {
        try
        {
            await operation;
            _context.TraceEvent($"grpc:call:server-completed-after-cancellation:{callId}");
        }
        catch (OperationCanceledException)
        {
            _context.TraceEvent($"grpc:call:server-cancelled:{callId}");
        }
        catch (Exception exception)
        {
            _context.TraceEvent($"grpc:call:server-failed-after-cancellation:{callId}:{exception.GetType().Name}");
        }
    }

    private async Task<TResponse> InvokeAttemptAsync<TRequest, TResponse>(
        long callId,
        int attempt,
        GrpcUnaryCall<TRequest> call,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var server = Resolve(call.Destination);
        var link = Between(call.Source, call.Destination);

        if (link.IsPartitioned || !server.IsAvailable)
        {
            throw new SimulationGrpcException(
                SimulationGrpcStatusCode.Unavailable,
                $"gRPC destination '{call.Destination}' is unavailable.");
        }

        var context = new GrpcCallContext
        {
            CallId = callId,
            Source = call.Source,
            Destination = call.Destination,
            Service = call.Service,
            Method = call.Method,
            Attempt = attempt
        };
        var delay = link.Latency;

        foreach (var fault in link.Evaluate(context))
        {
            switch (fault)
            {
                case DelayGrpcFault delayed:
                    delay += delayed.Delay;
                    break;
                case UnavailableGrpcFault:
                    throw new SimulationGrpcException(
                        SimulationGrpcStatusCode.Unavailable,
                        "Injected gRPC UNAVAILABLE fault.");
                default:
                    throw new InvalidOperationException($"Unsupported gRPC fault '{fault.GetType().FullName}'.");
            }
        }

        if (delay > TimeSpan.Zero)
        {
            _context.TraceEvent($"grpc:call:delayed:{callId}:attempt:{attempt}:{delay.Ticks}");
            await Task.Delay(delay, _context.TimeProvider, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (link.IsPartitioned || !server.IsAvailable)
        {
            throw new SimulationGrpcException(
                SimulationGrpcStatusCode.Unavailable,
                $"gRPC destination '{call.Destination}' became unavailable before dispatch.");
        }

        _context.TraceEvent($"grpc:call:dispatched:{callId}:attempt:{attempt}:{call.Destination}");
        var result = await server.InvokeAsync(
            call.Service,
            call.Method,
            call.Request!,
            typeof(TResponse),
            cancellationToken);
        return (TResponse)(result ?? throw new SimulationGrpcException(
            SimulationGrpcStatusCode.Internal,
            "gRPC handler returned null."));
    }

    private GrpcDeadlineScope? CreateDeadline(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (!timeout.HasValue)
        {
            return null;
        }

        if (timeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        return new GrpcDeadlineScope(timeout.Value, _context.TimeProvider, cancellationToken);
    }

    private static bool CanRetry(
        SimulationGrpcException exception,
        int attempt,
        SimulationGrpcCallOptions options)
    {
        return attempt < options.MaxAttempts && options.RetryableStatusCodes.Contains(exception.StatusCode);
    }

    private SimulationGrpcServer Resolve(string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        return _servers.TryGetValue(destination, out var server)
            ? server
            : throw new InvalidOperationException($"gRPC destination '{destination}' is not registered.");
    }

    private static void Validate(SimulationGrpcCallOptions options)
    {
        if (options.MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxAttempts must be greater than zero.");
        }

        if (options.RetryBackoff < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RetryBackoff cannot be negative.");
        }
    }
}
