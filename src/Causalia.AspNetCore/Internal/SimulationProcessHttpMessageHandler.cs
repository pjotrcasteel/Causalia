namespace Causalia.AspNetCore.Internal;

internal sealed class SimulationProcessHttpMessageHandler : HttpMessageHandler
{
    private readonly AspNetCoreSimulationProcess _process;

    public SimulationProcessHttpMessageHandler(AspNetCoreSimulationProcess process)
    {
        _process = process;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return SimulationHttpTaskBridge.Bridge(_process.SendAsync(request, cancellationToken));
    }
}
