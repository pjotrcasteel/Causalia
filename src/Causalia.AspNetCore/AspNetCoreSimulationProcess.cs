using Causalia.AspNetCore.Internal;
using Causalia.Nodes;
using Causalia.Processes;
using Microsoft.AspNetCore.Builder;

namespace Causalia.AspNetCore;

/// <summary>
/// Hosts restartable ASP.NET Core process generations with fresh DI and volatile state on every restart.
/// </summary>
public sealed class AspNetCoreSimulationProcess : IAsyncDisposable
{
    private readonly SimulationContext _context;
    private readonly AspNetCoreSimulationHostOptions _options;
    private readonly SimulationProcess<AspNetCoreProcessGeneration> _process;

    private AspNetCoreSimulationProcess(
        SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        SimulationProcess<AspNetCoreProcessGeneration> process)
    {
        _context = context;
        _options = options;
        _process = process;
    }

    /// <summary>
    /// Gets the stable base address used by every process generation.
    /// </summary>
    public Uri BaseAddress => _options.BaseAddress;

    /// <summary>
    /// Gets the stable simulated node backing this process.
    /// </summary>
    public SimulationNode Node => _process.Node;

    /// <summary>
    /// Gets the current process generation number.
    /// </summary>
    public int Generation => _process.Generation;

    /// <summary>
    /// Gets the current process lifecycle state.
    /// </summary>
    public SimulationProcessState State => _process.State;

    /// <summary>
    /// Gets whether the ASP.NET Core process currently has a running generation.
    /// </summary>
    public bool IsRunning => _process.IsRunning;

    /// <summary>
    /// Gets the service provider created for the current process generation.
    /// </summary>
    public IServiceProvider Services => _process.Current.Host.Services;

    /// <summary>
    /// Creates an HttpClient that follows this logical process across restarts.
    /// </summary>
    public HttpClient CreateClient()
    {
        return new HttpClient(new SimulationProcessHttpMessageHandler(this), disposeHandler: true)
        {
            BaseAddress = BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <summary>
    /// Gracefully stops the current ASP.NET Core process generation.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _process.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Abruptly crashes the current ASP.NET Core process generation without graceful host shutdown.
    /// </summary>
    public Task CrashAsync(CancellationToken cancellationToken)
    {
        return _process.CrashAsync(cancellationToken);
    }

    /// <summary>
    /// Rebuilds and starts a fresh ASP.NET Core generation with a new DI service provider.
    /// </summary>
    public Task RestartAsync(CancellationToken cancellationToken)
    {
        return _process.RestartAsync(cancellationToken);
    }

    /// <summary>
    /// Gracefully stops and disposes the process.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return _process.DisposeAsync();
    }

    internal AspNetCoreSimulationHost CurrentHost => _process.Current.Host;

    internal bool BelongsTo(SimulationContext context)
    {
        return ReferenceEquals(_context, context);
    }

    internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;

        await _process.RunAsync(
            async (generation, processCancellationToken) =>
            {
                response = await generation.Host.SendAsync(request, processCancellationToken);
            },
            cancellationToken);

        return response!;
    }

    internal static async Task<AspNetCoreSimulationProcess> StartAsync(
        SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        Action<WebApplicationBuilder>? configureBuilder,
        Action<WebApplication> configureApplication,
        CancellationToken cancellationToken)
    {
        var processOptions = new SimulationProcessOptions
        {
            Name = options.NodeName,
            TraceLifecycle = true
        };

        var process = await context.StartProcessAsync(
            processOptions,
            async (generationContext, generationCancellationToken) =>
            {
                var host = await AspNetCoreSimulationHost.StartOnNodeAsync(
                    context,
                    options,
                    configureBuilder,
                    configureApplication,
                    generationContext,
                    generationCancellationToken);
                return new AspNetCoreProcessGeneration(host);
            },
            cancellationToken);

        return new AspNetCoreSimulationProcess(context, options, process);
    }
}
