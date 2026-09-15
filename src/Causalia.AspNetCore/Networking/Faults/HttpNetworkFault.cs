namespace Causalia.AspNetCore.Networking.Faults;

/// <summary>
/// Represents one deterministic effect applied to a service-to-service HTTP request.
/// </summary>
public abstract class HttpNetworkFault
{
    private protected HttpNetworkFault()
    {
    }
}
