namespace Causalia;

/// <summary>
/// Explicitly allows nondeterministic APIs inside an otherwise deterministic simulation boundary.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
public sealed class AllowNondeterminismAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with the reason nondeterminism is intentionally allowed.
    /// </summary>
    public AllowNondeterminismAttribute(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason;
    }

    /// <summary>
    /// Gets the documented reason the deterministic boundary is intentionally relaxed.
    /// </summary>
    public string Reason { get; }
}
