using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace Causalia.AspNetCore.Internal;

internal sealed class SimulationServerApplication<TContext> : ISimulationServerApplication
    where TContext : notnull
{
    private readonly IHttpApplication<TContext> _application;

    public SimulationServerApplication(IHttpApplication<TContext> application)
    {
        _application = application;
    }

    public async Task ProcessAsync(IFeatureCollection features)
    {
        var context = _application.CreateContext(features);
        Exception? exception = null;

        try
        {
            await _application.ProcessRequestAsync(context);
        }
        catch (Exception caught)
        {
            exception = caught;
            throw;
        }
        finally
        {
            _application.DisposeContext(context, exception);
        }
    }
}
