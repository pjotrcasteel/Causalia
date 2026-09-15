namespace Causalia.FailureIntelligence;

/// <summary>
/// Groups multiple occurrences of the same semantic failure signature for deterministic triage.
/// </summary>
public sealed class FailureCluster
{
    internal FailureCluster(int triageRank, FailureSignature signature, IReadOnlyList<FailureAnalysis> occurrences, FailureAnalysis representative)
    {
        TriageRank = triageRank;
        Signature = signature;
        Occurrences = occurrences;
        Representative = representative;
        Seeds = occurrences.Select(value => value.OriginalFailure.Seed).Distinct().Order().ToList().AsReadOnly();
        Triggers = occurrences.Select(value => value.Trigger).Distinct().Order().ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the one-based position in the deterministic triage order.
    /// </summary>
    public int TriageRank { get; }

    /// <summary>
    /// Gets the semantic signature shared by every occurrence.
    /// </summary>
    public FailureSignature Signature { get; }

    /// <summary>
    /// Gets the semantic failure kind shared by every occurrence.
    /// </summary>
    public FailureKind Kind => Signature.Kind;

    /// <summary>
    /// Gets all analyzed occurrences in this cluster.
    /// </summary>
    public IReadOnlyList<FailureAnalysis> Occurrences { get; }

    /// <summary>
    /// Gets the number of analyzed occurrences in this cluster.
    /// </summary>
    public int OccurrenceCount => Occurrences.Count;

    /// <summary>
    /// Gets the distinct deterministic seeds that exposed this semantic failure.
    /// </summary>
    public IReadOnlyList<ulong> Seeds { get; }

    /// <summary>
    /// Gets the distinct trigger-attribution outcomes observed for this semantic failure.
    /// </summary>
    public IReadOnlyList<FailureTrigger> Triggers { get; }

    /// <summary>
    /// Gets the most actionable occurrence selected for diagnosis and replay.
    /// </summary>
    public FailureAnalysis Representative { get; }
}
