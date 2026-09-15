using Causalia.AspNetCore.Internal;
using Causalia.Nodes;
using Causalia.Processes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Causalia.AspNetCore;

/// <summary>
/// Hosts a real ASP.NET Core request pipeline on Causalia's deterministic in-memory transport.
/// </summary>
public sealed class AspNetCoreSimulationHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly SimulationContext _context;
    private readonly AspNetCoreSimulationHostOptions _options;
    private readonly SimulationServer _server;
    private int _activeRequests;
    private bool _disposed;
    private bool _stopped;
    private TaskCompletionSource<bool>? _idleSignal;

    private AspNetCoreSimulationHost(
        SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        WebApplication application,
        SimulationServer server,
        SimulationNode node)
    {
        _context = context;
        _options = options;
        _application = application;
        _server = server;
        Node = node;
    }

    /// <summary>
    /// Gets the base address used by relative requests and clients created by this host.
    /// </summary>
    public Uri BaseAddress => _options.BaseAddress;

    /// <summary>
    /// Gets the simulated node that owns this ASP.NET Core application.
    /// </summary>
    public SimulationNode Node { get; }

    /// <summary>
    /// Gets the ASP.NET Core service provider.
    /// </summary>
    public IServiceProvider Services => _application.Services;

    /// <summary>
    /// Creates an HttpClient whose traffic stays entirely inside the deterministic simulation transport.
    /// </summary>
    public HttpClient CreateClient()
    {
        return new HttpClient(new SimulationHttpMessageHandler(this), disposeHandler: true)
        {
            BaseAddress = BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <summary>
    /// Sends one HTTP request through the real ASP.NET Core middleware pipeline without using a network socket.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var requestUri = ResolveRequestUri(request.RequestUri);
        var traceTarget = requestUri.PathAndQuery;
        EnterRequest();
        Trace($"aspnetcore:request:accepted:{Node.Name}:{request.Method.Method}:{traceTarget}");
        HttpResponseMessage? response = null;

        try
        {
            await Node.RunAsync(
                async nodeCancellationToken =>
                {
                    Trace($"aspnetcore:request:dispatched:{Node.Name}:{request.Method.Method}:{traceTarget}");
                    response = await ProcessRequestAsync(request, requestUri, nodeCancellationToken);
                },
                cancellationToken);

            Trace($"aspnetcore:request:completed:{Node.Name}:{request.Method.Method}:{traceTarget}:{(int)response!.StatusCode}");
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !Node.IsRunning)
        {
            Trace($"aspnetcore:request:interrupted:{Node.Name}:{request.Method.Method}:{traceTarget}");
            throw;
        }
        catch
        {
            Trace($"aspnetcore:request:failed:{Node.Name}:{request.Method.Method}:{traceTarget}");
            throw;
        }
        finally
        {
            ExitRequest();
        }
    }

    /// <summary>
    /// Gracefully stops the hosted ASP.NET Core application while keeping its service provider available for disposal.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_stopped)
        {
            return;
        }

        await WaitForIdleAsync(cancellationToken);
        await _application.StopAsync(cancellationToken);
        _stopped = true;
        _context.TraceEvent($"aspnetcore:host:stopped:{Node.Name}:generation:{Node.Generation}");
    }

    /// <summary>
    /// Stops and disposes the hosted ASP.NET Core application.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (!_stopped)
        {
            await StopAsync(_context.CancellationToken);
        }

        _disposed = true;
        await _application.DisposeAsync();
    }

    internal async ValueTask DisposeAfterCrashAsync()
    {
        if (_disposed)
        {
            return;
        }

        await WaitForIdleAsync(_context.CancellationToken);
        _disposed = true;
        await _application.DisposeAsync();
        _context.TraceEvent($"aspnetcore:host:crash-disposed:{Node.Name}:generation:{Node.Generation}");
    }

    internal bool BelongsTo(SimulationContext context)
    {
        return ReferenceEquals(_context, context);
    }

    private void EnterRequest()
    {
        if (_activeRequests == 0)
        {
            _idleSignal = new TaskCompletionSource<bool>();
        }

        _activeRequests = checked(_activeRequests + 1);
    }

    private void ExitRequest()
    {
        _activeRequests--;

        if (_activeRequests == 0)
        {
            _idleSignal?.TrySetResult(true);
        }
    }

    private Task WaitForIdleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _activeRequests == 0 ? Task.CompletedTask : _idleSignal!.Task.WaitAsync(cancellationToken);
    }

    internal static Task<AspNetCoreSimulationHost> StartAsync(
        SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        Action<WebApplicationBuilder>? configureBuilder,
        Action<WebApplication> configureApplication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return StartCoreAsync(context, options, configureBuilder, configureApplication, null, cancellationToken);
    }

    internal static Task<AspNetCoreSimulationHost> StartOnNodeAsync(
        SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        Action<WebApplicationBuilder>? configureBuilder,
        Action<WebApplication> configureApplication,
        SimulationProcessGenerationContext generationContext,
        CancellationToken cancellationToken)
    {
        return StartCoreAsync(
            context,
            options,
            configureBuilder,
            configureApplication,
            generationContext,
            cancellationToken);
    }

    private static async Task<AspNetCoreSimulationHost> StartCoreAsync(
        SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        Action<WebApplicationBuilder>? configureBuilder,
        Action<WebApplication> configureApplication,
        SimulationProcessGenerationContext? generationContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configureApplication);
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();
        var node = generationContext?.Node ?? context.CreateNode(options.NodeName);

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                Args = Array.Empty<string>(),
                EnvironmentName = "Causalia"
            });

        configureBuilder?.Invoke(builder);
        var server = new SimulationServer(options.BaseAddress);
        builder.Services.AddSingleton<IServer>(server);
        builder.Services.AddSingleton(context.TimeProvider);
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton(node);

        if (generationContext is not null)
        {
            builder.Services.AddSingleton(generationContext);
        }

        var application = builder.Build();
        configureApplication(application);
        var host = new AspNetCoreSimulationHost(context, options, application, server, node);

        try
        {
            await application.StartAsync(cancellationToken);
            context.TraceEvent($"aspnetcore:host:started:{node.Name}:generation:{node.Generation}:{options.BaseAddress}");
            return host;
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    private async Task<HttpResponseMessage> ProcessRequestAsync(
        HttpRequestMessage request,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        await using var requestBody = new MemoryStream();

        if (request.Content is not null)
        {
            await request.Content.CopyToAsync(requestBody, cancellationToken);
            requestBody.Position = 0;
        }

        await using var responseBody = new MemoryStream();
        using var lifetime = new SimulationRequestLifetimeFeature(cancellationToken);
        var responseFeature = new SimulationResponseFeature(responseBody);
        var responseBodyFeature = new SimulationResponseBodyFeature(responseBody, responseFeature);
        var features = CreateFeatures(request, requestUri, requestBody, responseFeature, responseBodyFeature, lifetime);
        Exception? exception = null;

        try
        {
            await _server.ProcessAsync(features);
            await responseBodyFeature.CompleteAsync();
        }
        catch (Exception caught)
        {
            exception = caught;
            throw;
        }
        finally
        {
            if (exception is not null && !responseFeature.HasStarted)
            {
                responseFeature.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }

        responseBody.Position = 0;
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)responseFeature.StatusCode)
        {
            ReasonPhrase = responseFeature.ReasonPhrase,
            RequestMessage = request,
            Content = new ByteArrayContent(responseBody.ToArray())
        };

        CopyResponseHeaders(responseFeature.Headers, response);
        return response;
    }

    private static IFeatureCollection CreateFeatures(
        HttpRequestMessage request,
        Uri requestUri,
        Stream requestBody,
        SimulationResponseFeature responseFeature,
        SimulationResponseBodyFeature responseBodyFeature,
        SimulationRequestLifetimeFeature lifetime)
    {
        var requestFeature = new HttpRequestFeature
        {
            Method = request.Method.Method,
            Scheme = requestUri.Scheme,
            PathBase = string.Empty,
            Path = requestUri.AbsolutePath,
            QueryString = requestUri.Query,
            RawTarget = requestUri.PathAndQuery,
            Protocol = $"HTTP/{request.Version}",
            Headers = CreateRequestHeaders(request, requestUri),
            Body = requestBody
        };
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(requestFeature);
        features.Set<IHttpResponseFeature>(responseFeature);
        features.Set<IHttpResponseBodyFeature>(responseBodyFeature);
        features.Set<IHttpRequestLifetimeFeature>(lifetime);
        return features;
    }

    private static IHeaderDictionary CreateRequestHeaders(HttpRequestMessage request, Uri requestUri)
    {
        var headers = new HeaderDictionary();

        foreach (var header in request.Headers)
        {
            headers[header.Key] = new StringValues(header.Value.ToArray());
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = new StringValues(header.Value.ToArray());
            }
        }

        if (!headers.ContainsKey("Host"))
        {
            headers["Host"] = requestUri.IsDefaultPort ? requestUri.Host : requestUri.Authority;
        }

        return headers;
    }

    private static void CopyResponseHeaders(IHeaderDictionary source, HttpResponseMessage response)
    {
        foreach (var header in source)
        {
            var values = header.Value.ToArray();

            if (!response.Headers.TryAddWithoutValidation(header.Key, values))
            {
                response.Content.Headers.TryAddWithoutValidation(header.Key, values);
            }
        }
    }

    private Uri ResolveRequestUri(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return BaseAddress;
        }

        return requestUri.IsAbsoluteUri ? requestUri : new Uri(BaseAddress, requestUri);
    }

    private void Trace(string message)
    {
        if (_options.TraceRequests)
        {
            _context.TraceEvent(message);
        }
    }

    private static void ValidateOptions(AspNetCoreSimulationHostOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.NodeName);

        if (!options.BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("ASP.NET Core simulation BaseAddress must be absolute.", nameof(options));
        }

        if (options.BaseAddress.Scheme is not "http" and not "https")
        {
            throw new ArgumentException("ASP.NET Core simulation BaseAddress must use HTTP or HTTPS.", nameof(options));
        }
    }
}
