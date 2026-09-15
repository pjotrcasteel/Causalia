namespace Causalia.Processes;

/// <summary>
/// Represents one volatile generation of a simulated process.
/// </summary>
public interface ISimulationProcessGeneration : IAsyncDisposable
{
    /// <summary>
    /// Starts the process generation after it has been created.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Performs graceful shutdown work for the process generation.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
