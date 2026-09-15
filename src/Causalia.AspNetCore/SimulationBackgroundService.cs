using Causalia.Processes;
using Microsoft.Extensions.Hosting;

namespace Causalia.AspNetCore;

/// <summary>
/// Runs long-lived hosted work inside a Causalia process generation instead of escaping to uncontrolled background execution.
/// </summary>
public abstract class SimulationBackgroundService : IHostedService
{
    private readonly SimulationProcessGenerationContext _generation;

    /// <summary>
    /// Initializes a deterministic generation-scoped background service.
    /// </summary>
    protected SimulationBackgroundService(SimulationProcessGenerationContext generation)
    {
        _generation = generation;
    }

    /// <summary>
    /// Starts the generation-scoped background operation.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = _generation.RunBackgroundAsync(ExecuteAsync);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes the host stop hook after generation cancellation has been requested by the owning simulated process.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes deterministic long-lived work for the current process generation.
    /// </summary>
    protected abstract Task ExecuteAsync(CancellationToken cancellationToken);
}
