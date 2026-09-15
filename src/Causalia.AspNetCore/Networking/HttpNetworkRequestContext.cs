namespace Causalia.AspNetCore.Networking;

/// <summary>
/// Describes one logical service-to-service HTTP request presented to deterministic network fault policies.
/// </summary>
public sealed class HttpNetworkRequestContext
{
    internal HttpNetworkRequestContext(long requestId, string sourceService, string destinationService, HttpMethod method, Uri requestUri)
    {
        RequestId = requestId;
        SourceService = sourceService;
        DestinationService = destinationService;
        Method = method;
        RequestUri = requestUri;
    }

    /// <summary>
    /// Gets the stable logical request identifier within this network instance.
    /// </summary>
    public long RequestId { get; }

    /// <summary>
    /// Gets the logical source service name.
    /// </summary>
    public string SourceService { get; }

    /// <summary>
    /// Gets the logical destination service name.
    /// </summary>
    public string DestinationService { get; }

    /// <summary>
    /// Gets the HTTP method.
    /// </summary>
    public HttpMethod Method { get; }

    /// <summary>
    /// Gets the resolved absolute request URI.
    /// </summary>
    public Uri RequestUri { get; }
}
