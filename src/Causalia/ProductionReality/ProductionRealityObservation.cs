namespace Causalia.ProductionReality;

/// <summary>
/// Represents one normalized operation observed in production.
/// </summary>
public sealed class ProductionRealityObservation
{
    /// <summary>
    /// Gets or initializes the stable observation identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the stable logical operation name.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets or initializes the production start instant.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets or initializes the observed production duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or initializes the observed outcome.
    /// </summary>
    public ProductionRealityOutcome Outcome { get; init; }

    /// <summary>
    /// Gets or initializes the correlation identifier used to group an incident or request flow.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets or initializes normalized string attributes carried with the observation.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
}
