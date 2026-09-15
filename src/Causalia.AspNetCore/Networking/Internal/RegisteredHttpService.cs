using Causalia.Nodes;

namespace Causalia.AspNetCore.Networking.Internal;

internal sealed class RegisteredHttpService
{
    private readonly AspNetCoreSimulationHost? _host;
    private readonly AspNetCoreSimulationProcess? _process;

    public RegisteredHttpService(string name, AspNetCoreSimulationHost host)
    {
        Name = name;
        _host = host;
    }

    public RegisteredHttpService(string name, AspNetCoreSimulationProcess process)
    {
        Name = name;
        _process = process;
    }

    public string Name { get; }

    public Uri BaseAddress => _host?.BaseAddress ?? _process!.BaseAddress;

    public SimulationNode Node => _host?.Node ?? _process!.Node;

    public bool IsAvailable => _host?.Node.IsRunning ?? _process!.IsRunning;

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _host is not null
            ? _host.SendAsync(request, cancellationToken)
            : _process!.SendAsync(request, cancellationToken);
    }
}
