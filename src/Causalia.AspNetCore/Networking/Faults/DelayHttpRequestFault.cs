namespace Causalia.AspNetCore.Networking.Faults;

/// <summary>
/// Adds deterministic virtual transport latency to one logical HTTP request.
/// </summary>
public sealed class DelayHttpRequestFault : HttpNetworkFault
{
    internal DelayHttpRequestFault(TimeSpan delay)
    {
        Delay = delay;
    }

    /// <summary>
    /// Gets the additional virtual latency.
    /// </summary>
    public TimeSpan Delay { get; }
}
