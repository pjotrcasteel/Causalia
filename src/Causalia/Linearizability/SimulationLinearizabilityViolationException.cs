namespace Causalia.Linearizability;

/// <summary>
/// Indicates that no legal sequential explanation exists for a completed concurrent history.
/// </summary>
public sealed class SimulationLinearizabilityViolationException : SimulationLinearizabilityException
{
    internal SimulationLinearizabilityViolationException(string historyName, LinearizabilityResult result)
        : base(
            $"Linearizability history '{historyName}' is not linearizable after exploring {result.SearchStates} search states.",
            historyName,
            result)
    {
    }
}
