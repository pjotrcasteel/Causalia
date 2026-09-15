namespace Causalia.AspNetCore;

/// <summary>
/// Configures one deterministic in-memory ASP.NET Core application hosted inside a Causalia simulation.
/// </summary>
public sealed class AspNetCoreSimulationHostOptions
{
    /// <summary>
    /// Gets or sets the logical simulated node name used by the hosted application.
    /// </summary>
    public string NodeName { get; set; } = "aspnetcore";

    /// <summary>
    /// Gets or sets the base address used to resolve relative request URIs.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("http://causalia.local/");

    /// <summary>
    /// Gets or sets whether request lifecycle events are written to the Causalia trace.
    /// </summary>
    public bool TraceRequests { get; set; } = true;
}
