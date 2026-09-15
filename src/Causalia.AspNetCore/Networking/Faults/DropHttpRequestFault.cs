namespace Causalia.AspNetCore.Networking.Faults;

/// <summary>
/// Drops a logical HTTP request before it reaches the destination service.
/// </summary>
public sealed class DropHttpRequestFault : HttpNetworkFault
{
    internal DropHttpRequestFault()
    {
    }
}
