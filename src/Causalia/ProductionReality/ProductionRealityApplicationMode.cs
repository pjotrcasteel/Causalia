namespace Causalia.ProductionReality;

/// <summary>
/// Describes how production evidence was applied to a deterministic simulation.
/// </summary>
public enum ProductionRealityApplicationMode
{
    /// <summary>
    /// One observation was selected from a production-derived operation profile.
    /// </summary>
    Sampled,

    /// <summary>
    /// One observation was replayed as part of a correlated production incident.
    /// </summary>
    IncidentReplay
}
