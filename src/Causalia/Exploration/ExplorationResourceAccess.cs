namespace Causalia.Exploration;

/// <summary>
/// Describes one declared shared-resource access used by partial-order reduction.
/// </summary>
public sealed class ExplorationResourceAccess
{
    internal ExplorationResourceAccess(string resource, ExplorationAccessKind kind)
    {
        Resource = resource;
        Kind = kind;
    }

    /// <summary>
    /// Gets the stable logical resource key.
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Gets the kind of access performed against the resource.
    /// </summary>
    public ExplorationAccessKind Kind { get; }
}
