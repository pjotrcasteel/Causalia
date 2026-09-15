namespace Causalia.Linearizability;

/// <summary>
/// Base exception for deterministic linearizability failures.
/// </summary>
public abstract class SimulationLinearizabilityException : Exception
{
    /// <summary>
    /// Initializes a linearizability failure for the supplied history and bounded-check result.
    /// </summary>
    protected SimulationLinearizabilityException(string message, string historyName, LinearizabilityResult result)
        : base(message)
    {
        HistoryName = historyName;
        Result = result;
    }

    /// <summary>
    /// Gets the history that failed linearizability validation.
    /// </summary>
    public string HistoryName { get; }

    /// <summary>
    /// Gets the bounded check result.
    /// </summary>
    public LinearizabilityResult Result { get; }
}
