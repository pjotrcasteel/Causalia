using Causalia.AspNetCore.Networking;
using Microsoft.AspNetCore.Builder;

namespace Causalia.AspNetCore;

/// <summary>
/// Adds deterministic ASP.NET Core hosting to a simulation context.
/// </summary>
public static class SimulationContextAspNetCoreExtensions
{
    /// <summary>
    /// Creates a deterministic service-to-service HTTP network for ASP.NET Core simulation hosts.
    /// </summary>
    public static SimulationHttpNetwork CreateHttpNetwork(
        this SimulationContext context,
        SimulationHttpNetworkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimulationHttpNetwork(context, options ?? new SimulationHttpNetworkOptions());
    }

    /// <summary>
    /// Starts an in-memory ASP.NET Core application using default Causalia host options.
    /// </summary>
    public static Task<AspNetCoreSimulationHost> StartAspNetCoreAsync(
        this SimulationContext context,
        Action<WebApplication> configureApplication,
        CancellationToken cancellationToken)
    {
        return AspNetCoreSimulationHost.StartAsync(
            context,
            new AspNetCoreSimulationHostOptions(),
            null,
            configureApplication,
            cancellationToken);
    }

    /// <summary>
    /// Starts an in-memory ASP.NET Core application with deterministic time and a simulated node lifecycle.
    /// </summary>
    public static Task<AspNetCoreSimulationHost> StartAspNetCoreAsync(
        this SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        Action<WebApplicationBuilder>? configureBuilder,
        Action<WebApplication> configureApplication,
        CancellationToken cancellationToken)
    {
        return AspNetCoreSimulationHost.StartAsync(context, options, configureBuilder, configureApplication, cancellationToken);
    }

    /// <summary>
    /// Starts a restartable ASP.NET Core process whose DI container and volatile application state are rebuilt after restart.
    /// </summary>
    public static Task<AspNetCoreSimulationProcess> StartAspNetCoreProcessAsync(
        this SimulationContext context,
        Action<WebApplication> configureApplication,
        CancellationToken cancellationToken)
    {
        return AspNetCoreSimulationProcess.StartAsync(
            context,
            new AspNetCoreSimulationHostOptions(),
            null,
            configureApplication,
            cancellationToken);
    }

    /// <summary>
    /// Starts a restartable ASP.NET Core process with deterministic time and fresh DI state after every restart.
    /// </summary>
    public static Task<AspNetCoreSimulationProcess> StartAspNetCoreProcessAsync(
        this SimulationContext context,
        AspNetCoreSimulationHostOptions options,
        Action<WebApplicationBuilder>? configureBuilder,
        Action<WebApplication> configureApplication,
        CancellationToken cancellationToken)
    {
        return AspNetCoreSimulationProcess.StartAsync(context, options, configureBuilder, configureApplication, cancellationToken);
    }
}
