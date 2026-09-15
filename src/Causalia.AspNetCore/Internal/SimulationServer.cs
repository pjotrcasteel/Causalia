using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Causalia.AspNetCore.Internal;

internal sealed class SimulationServer : IServer
{
    private ISimulationServerApplication? _application;

    public SimulationServer(Uri baseAddress)
    {
        var addresses = new ServerAddressesFeature();
        addresses.Addresses.Add(baseAddress.ToString());
        Features.Set<IServerAddressesFeature>(addresses);
    }

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(application);
        cancellationToken.ThrowIfCancellationRequested();
        _application = new SimulationServerApplication<TContext>(application);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _application = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _application = null;
    }

    public Task ProcessAsync(IFeatureCollection features)
    {
        return (_application ?? throw new InvalidOperationException("The ASP.NET Core simulation server has not been started."))
            .ProcessAsync(features);
    }
}
