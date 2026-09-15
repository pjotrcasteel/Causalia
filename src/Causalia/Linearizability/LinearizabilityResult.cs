namespace Causalia.Linearizability;

/// <summary>
/// Describes the result of checking a completed concurrent history against a sequential specification.
/// </summary>
public sealed class LinearizabilityResult
{
    internal LinearizabilityResult(
        LinearizabilityStatus status,
        LinearizabilitySearchMetrics metrics,
        IReadOnlyList<long> linearization,
        IReadOnlyList<long> longestPrefix,
        string? reason)
    {
        Status = status;
        OperationsChecked = metrics.OperationsChecked;
        PendingOperations = metrics.PendingOperations;
        SearchStates = metrics.SearchStates;
        Linearization = linearization;
        LongestPrefix = longestPrefix;
        Reason = reason;
    }

    /// <summary>
    /// Gets the bounded-check outcome.
    /// </summary>
    public LinearizabilityStatus Status { get; }

    /// <summary>
    /// Gets whether a complete legal sequential explanation was found.
    /// </summary>
    public bool IsLinearizable => Status == LinearizabilityStatus.Linearizable;

    /// <summary>
    /// Gets the number of completed operations included in the check.
    /// </summary>
    public int OperationsChecked { get; }

    /// <summary>
    /// Gets the number of pending operations omitted from the completed-history check.
    /// </summary>
    public int PendingOperations { get; }

    /// <summary>
    /// Gets the number of distinct bounded search states explored.
    /// </summary>
    public int SearchStates { get; }

    /// <summary>
    /// Gets a complete legal linearization when the history is linearizable.
    /// </summary>
    public IReadOnlyList<long> Linearization { get; }

    /// <summary>
    /// Gets the longest legal partial linearization discovered during search.
    /// </summary>
    public IReadOnlyList<long> LongestPrefix { get; }

    /// <summary>
    /// Gets a diagnostic reason when the result is inconclusive.
    /// </summary>
    public string? Reason { get; }
}
