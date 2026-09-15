namespace Causalia.Linearizability;

/// <summary>
/// Indicates that configured linearizability search bounds were exhausted before a proof could be reached.
/// </summary>
public sealed class SimulationLinearizabilityInconclusiveException : SimulationLinearizabilityException
{
    internal SimulationLinearizabilityInconclusiveException(string historyName, LinearizabilityResult result)
        : base(
            $"Linearizability history '{historyName}' was inconclusive. {result.Reason}",
            historyName,
            result)
    {
    }
}
