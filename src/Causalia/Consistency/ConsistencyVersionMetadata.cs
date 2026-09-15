namespace Causalia.Consistency;

/// <summary>
/// Carries internal causal metadata used to construct one logical consistency version.
/// </summary>
internal sealed class ConsistencyVersionMetadata
{
    public required IReadOnlyList<long> CausalPredecessorIds { get; init; }

    public required IReadOnlyList<long> ReadPredecessorIds { get; init; }

    public required int SessionWriteSequence { get; init; }
}
