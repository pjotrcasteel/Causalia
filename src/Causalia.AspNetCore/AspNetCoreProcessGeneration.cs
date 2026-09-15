using Causalia.Processes;

namespace Causalia.AspNetCore;

internal sealed class AspNetCoreProcessGeneration : ISimulationProcessGeneration
{
    private bool _stopped;

    public AspNetCoreProcessGeneration(AspNetCoreSimulationHost host)
    {
        Host = host;
    }

    public AspNetCoreSimulationHost Host { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Host.StopAsync(cancellationToken);
        _stopped = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stopped)
        {
            await Host.DisposeAsync();
            return;
        }

        await Host.DisposeAfterCrashAsync();
    }
}
