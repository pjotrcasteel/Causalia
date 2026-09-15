namespace Causalia.Consistency;

/// <summary>
/// Summarizes one consistency history that completed successfully.
/// </summary>
public sealed class ConsistencyOutcome
{
    internal ConsistencyOutcome(
        string historyName,
        ConsistencyGuarantees guarantees,
        int writes,
        int reads,
        int replicaObservations,
        IReadOnlyList<ConsistencyConvergenceOutcome> convergence)
    {
        HistoryName = historyName;
        Guarantees = guarantees;
        Writes = writes;
        Reads = reads;
        ReplicaObservations = replicaObservations;
        Convergence = Array.AsReadOnly(convergence.ToArray());
    }

    /// <summary>
    /// Gets the logical history name.
    /// </summary>
    public string HistoryName { get; }

    /// <summary>
    /// Gets the consistency guarantees enabled for the history.
    /// </summary>
    public ConsistencyGuarantees Guarantees { get; }

    /// <summary>
    /// Gets the number of logical writes recorded in the history.
    /// </summary>
    public int Writes { get; }

    /// <summary>
    /// Gets the number of logical reads recorded in the history.
    /// </summary>
    public int Reads { get; }

    /// <summary>
    /// Gets the number of complete replica snapshots observed in the history.
    /// </summary>
    public int ReplicaObservations { get; }

    /// <summary>
    /// Gets the bounded convergence requirements that completed successfully.
    /// </summary>
    public IReadOnlyList<ConsistencyConvergenceOutcome> Convergence { get; }
}
