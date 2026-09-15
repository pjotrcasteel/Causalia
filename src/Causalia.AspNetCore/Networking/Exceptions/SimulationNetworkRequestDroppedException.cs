namespace Causalia.AspNetCore.Networking.Exceptions;

/// <summary>
/// Indicates that deterministic network fault injection dropped an HTTP request.
/// </summary>
public sealed class SimulationNetworkRequestDroppedException : HttpRequestException
{
    internal SimulationNetworkRequestDroppedException(long requestId, string sourceService, string destinationService)
        : base($"Simulated HTTP request {requestId} from '{sourceService}' to '{destinationService}' was dropped.")
    {
        RequestId = requestId;
        SourceService = sourceService;
        DestinationService = destinationService;
    }

    /// <summary>
    /// Gets the logical request identifier.
    /// </summary>
    public long RequestId { get; }

    /// <summary>
    /// Gets the logical source service.
    /// </summary>
    public string SourceService { get; }

    /// <summary>
    /// Gets the logical destination service.
    /// </summary>
    public string DestinationService { get; }
}
