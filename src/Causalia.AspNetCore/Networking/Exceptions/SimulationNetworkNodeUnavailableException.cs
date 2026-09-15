namespace Causalia.AspNetCore.Networking.Exceptions;

/// <summary>
/// Indicates that a source or destination service node is not running when an HTTP delivery is attempted.
/// </summary>
public sealed class SimulationNetworkNodeUnavailableException : HttpRequestException
{
    internal SimulationNetworkNodeUnavailableException(string serviceName, string role)
        : base($"The simulated HTTP {role} service '{serviceName}' is not running.")
    {
        ServiceName = serviceName;
        Role = role;
    }

    /// <summary>
    /// Gets the logical service name.
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// Gets whether the unavailable service was the source or destination.
    /// </summary>
    public string Role { get; }
}
