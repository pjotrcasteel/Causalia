using Causalia.AspNetCore.Networking.Exceptions;
using Causalia.AspNetCore.Networking.Faults;
using Causalia.AspNetCore.Networking.Internal;

namespace Causalia.AspNetCore.Networking;

/// <summary>
/// Routes deterministic service-to-service HTTP traffic between ASP.NET Core simulation hosts.
/// </summary>
public sealed class SimulationHttpNetwork
{
    private readonly SimulationContext _context;
    private readonly Dictionary<(string Source, string Destination), SimulationHttpNetworkLink> _links = new();
    private readonly SimulationHttpNetworkOptions _options;
    private readonly Dictionary<string, RegisteredHttpService> _services = new(StringComparer.Ordinal);
    private long _nextRequestId;

    internal SimulationHttpNetwork(SimulationContext context, SimulationHttpNetworkOptions options)
    {
        _context = context;
        _options = options;
    }

    /// <summary>
    /// Registers one ASP.NET Core host under a stable logical service name.
    /// </summary>
    public void Register(string serviceName, AspNetCoreSimulationHost host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(host);

        if (!host.BelongsTo(_context))
        {
            throw new ArgumentException("The ASP.NET Core host belongs to a different simulation context.", nameof(host));
        }

        if (!_services.TryAdd(serviceName, new RegisteredHttpService(serviceName, host)))
        {
            throw new InvalidOperationException($"An HTTP service named '{serviceName}' is already registered.");
        }

        Trace($"http-network:registered:{serviceName}:{host.Node.Name}:{host.BaseAddress}");
    }

    /// <summary>
    /// Registers one restartable ASP.NET Core process under a stable logical service name.
    /// </summary>
    public void Register(string serviceName, AspNetCoreSimulationProcess process)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(process);

        if (!process.BelongsTo(_context))
        {
            throw new ArgumentException("The ASP.NET Core process belongs to a different simulation context.", nameof(process));
        }

        if (!_services.TryAdd(serviceName, new RegisteredHttpService(serviceName, process)))
        {
            throw new InvalidOperationException($"An HTTP service named '{serviceName}' is already registered.");
        }

        Trace($"http-network:registered-process:{serviceName}:{process.Node.Name}:{process.BaseAddress}");
    }

    /// <summary>
    /// Gets or creates the directed link from one registered service to another.
    /// </summary>
    public SimulationHttpNetworkLink Between(string sourceService, string destinationService)
    {
        ResolveService(sourceService);
        ResolveService(destinationService);
        var key = (sourceService, destinationService);

        if (_links.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var link = new SimulationHttpNetworkLink(_context, sourceService, destinationService);
        _links.Add(key, link);
        return link;
    }

    /// <summary>
    /// Creates an HttpClient that routes from one registered service to one registered destination without sockets.
    /// </summary>
    public HttpClient CreateClient(string sourceService, string destinationService)
    {
        ResolveService(sourceService);
        var destination = ResolveService(destinationService);
        var client = new HttpClient(new SimulationNetworkHttpMessageHandler(this, sourceService, destinationService), disposeHandler: true)
        {
            BaseAddress = destination.BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        };
        return client;
    }

    internal async Task<HttpResponseMessage> SendAsync(
        string sourceService,
        string destinationService,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var source = ResolveService(sourceService);
        var destination = ResolveService(destinationService);
        var requestUri = ValidateRequestUri(destination, request.RequestUri);
        var requestId = checked(++_nextRequestId);
        var snapshot = await HttpRequestSnapshot.CreateAsync(request, cancellationToken);
        var requestContext = new HttpNetworkRequestContext(requestId, sourceService, destinationService, request.Method, requestUri);
        Trace($"http-network:request:accepted:{requestId}:{sourceService}->{destinationService}:{request.Method.Method}:{requestUri.PathAndQuery}");

        if (!source.IsAvailable)
        {
            Trace($"http-network:request:source-unavailable:{requestId}:{sourceService}");
            throw new SimulationNetworkNodeUnavailableException(sourceService, "source");
        }

        HttpResponseMessage? response = null;

        try
        {
            await source.Node.RunAsync(
                async sourceCancellationToken =>
                {
                    response = await SendCoreAsync(source, destination, requestContext, snapshot, sourceCancellationToken);
                },
                cancellationToken);
            response!.RequestMessage = request;
            return response!;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !source.IsAvailable)
        {
            Trace($"http-network:request:source-interrupted:{requestId}:{sourceService}->{destinationService}");
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        RegisteredHttpService source,
        RegisteredHttpService destination,
        HttpNetworkRequestContext requestContext,
        HttpRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var link = Between(source.Name, destination.Name);

        if (link.IsPartitioned)
        {
            Trace($"http-network:request:partitioned:{requestContext.RequestId}:{source.Name}->{destination.Name}");
            throw new SimulationNetworkPartitionException(source.Name, destination.Name);
        }

        var faults = link.Evaluate(requestContext);
        var delay = link.BaseLatency;
        var duplicateCount = 0;
        var dropped = false;

        foreach (var fault in faults)
        {
            switch (fault)
            {
                case DelayHttpRequestFault delayFault:
                    delay += delayFault.Delay;
                    break;
                case DuplicateHttpRequestFault duplicateFault:
                    duplicateCount = checked(duplicateCount + duplicateFault.AdditionalCopies);
                    break;
                case DropHttpRequestFault:
                    dropped = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported HTTP network fault '{fault.GetType().FullName}'.");
            }
        }

        if (delay > TimeSpan.Zero)
        {
            Trace($"http-network:request:delayed:{requestContext.RequestId}:{delay.Ticks}");
            await Task.Delay(delay, _context.TimeProvider, cancellationToken);
        }

        if (dropped)
        {
            Trace($"http-network:request:dropped:{requestContext.RequestId}:{source.Name}->{destination.Name}");
            throw new SimulationNetworkRequestDroppedException(requestContext.RequestId, source.Name, destination.Name);
        }

        if (!destination.IsAvailable)
        {
            Trace($"http-network:request:destination-unavailable:{requestContext.RequestId}:{destination.Name}");
            throw new SimulationNetworkNodeUnavailableException(destination.Name, "destination");
        }

        var primaryTask = DeliverAsync(destination, requestContext, snapshot, attempt: 1, cancellationToken);

        if (duplicateCount > 0)
        {
            Trace($"http-network:request:duplicated:{requestContext.RequestId}:additional:{duplicateCount}");
        }

        for (var copyIndex = 0; copyIndex < duplicateCount; copyIndex++)
        {
            var duplicateTask = DeliverAndDisposeAsync(
                destination,
                requestContext,
                snapshot,
                attempt: copyIndex + 2,
                cancellationToken);
            _context.TrackOperation(duplicateTask);
        }

        return await primaryTask;
    }

    private async Task<HttpResponseMessage> DeliverAsync(
        RegisteredHttpService destination,
        HttpNetworkRequestContext requestContext,
        HttpRequestSnapshot snapshot,
        int attempt,
        CancellationToken cancellationToken)
    {
        using var request = snapshot.CreateMessage();
        Trace($"http-network:request:delivered:{requestContext.RequestId}:attempt:{attempt}:{destination.Name}");
        var response = await destination.SendAsync(request, cancellationToken);
        response.RequestMessage = null;
        Trace($"http-network:request:completed:{requestContext.RequestId}:attempt:{attempt}:{(int)response.StatusCode}");
        return response;
    }

    private async Task DeliverAndDisposeAsync(
        RegisteredHttpService destination,
        HttpNetworkRequestContext requestContext,
        HttpRequestSnapshot snapshot,
        int attempt,
        CancellationToken cancellationToken)
    {
        using var response = await DeliverAsync(destination, requestContext, snapshot, attempt, cancellationToken);
    }

    private RegisteredHttpService ResolveService(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        if (_services.TryGetValue(serviceName, out var service))
        {
            return service;
        }

        throw new InvalidOperationException($"HTTP service '{serviceName}' is not registered in this simulation network.");
    }

    private static Uri ValidateRequestUri(RegisteredHttpService destination, Uri? requestUri)
    {
        if (requestUri is null)
        {
            return destination.BaseAddress;
        }

        var resolved = requestUri.IsAbsoluteUri ? requestUri : new Uri(destination.BaseAddress, requestUri);

        if (!string.Equals(resolved.Scheme, destination.BaseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(resolved.Authority, destination.BaseAddress.Authority, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The HTTP client targets service '{destination.Name}', but request URI '{resolved}' does not use its registered base address " +
                $"'{destination.BaseAddress}'.");
        }

        return resolved;
    }

    private void Trace(string message)
    {
        if (_options.TraceRequests)
        {
            _context.TraceEvent(message);
        }
    }
}
