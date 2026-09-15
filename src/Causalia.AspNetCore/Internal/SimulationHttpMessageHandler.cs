namespace Causalia.AspNetCore.Internal;

internal sealed class SimulationHttpMessageHandler : HttpMessageHandler
{
    private readonly AspNetCoreSimulationHost _host;

    public SimulationHttpMessageHandler(AspNetCoreSimulationHost host)
    {
        _host = host;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return SimulationHttpTaskBridge.Bridge(_host.SendAsync(request, cancellationToken));
    }
}
