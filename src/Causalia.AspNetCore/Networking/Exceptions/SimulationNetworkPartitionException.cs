namespace Causalia.AspNetCore.Networking.Exceptions;

/// <summary>
/// Indicates that an HTTP request could not cross a currently partitioned simulated link.
/// </summary>
public sealed class SimulationNetworkPartitionException : HttpRequestException
{
    internal SimulationNetworkPartitionException(string sourceService, string destinationService)
        : base($"The simulated HTTP link from '{sourceService}' to '{destinationService}' is partitioned.")
    {
        SourceService = sourceService;
        DestinationService = destinationService;
    }

    /// <summary>
    /// Gets the logical source service.
    /// </summary>
    public string SourceService { get; }

    /// <summary>
    /// Gets the logical destination service.
    /// </summary>
    public string DestinationService { get; }
}
