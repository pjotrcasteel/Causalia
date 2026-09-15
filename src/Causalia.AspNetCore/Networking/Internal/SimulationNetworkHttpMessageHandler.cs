using Causalia.AspNetCore.Internal;

namespace Causalia.AspNetCore.Networking.Internal;

internal sealed class SimulationNetworkHttpMessageHandler : HttpMessageHandler
{
    private readonly string _destinationService;
    private readonly SimulationHttpNetwork _network;
    private readonly string _sourceService;

    public SimulationNetworkHttpMessageHandler(SimulationHttpNetwork network, string sourceService, string destinationService)
    {
        _network = network;
        _sourceService = sourceService;
        _destinationService = destinationService;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var operation = _network.SendAsync(_sourceService, _destinationService, request, cancellationToken);
        return SimulationHttpTaskBridge.Bridge(operation);
    }
}
