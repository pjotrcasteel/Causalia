namespace Causalia.FailureIntelligence;

/// <summary>
/// Contains semantic failure clusters ordered for deterministic engineering triage.
/// </summary>
public sealed class FailureIntelligenceReport
{
    internal FailureIntelligenceReport(IReadOnlyList<FailureCluster> clusters, int totalOccurrences)
    {
        Clusters = clusters;
        TotalOccurrences = totalOccurrences;
    }

    /// <summary>
    /// Gets semantic failure clusters in deterministic triage order.
    /// More frequent failures sort first, followed by stronger analysis confidence and smaller reproductions.
    /// </summary>
    public IReadOnlyList<FailureCluster> Clusters { get; }

    /// <summary>
    /// Gets the total number of analyzed failure occurrences represented by this report.
    /// </summary>
    public int TotalOccurrences { get; }

    /// <summary>
    /// Gets the number of distinct semantic failure signatures.
    /// </summary>
    public int UniqueFailureCount => Clusters.Count;
}
