using Causalia.Faults;
using Causalia.Minimization;

namespace Causalia.Runtime;

internal sealed class FaultExecutionController
{
    private readonly HashSet<FaultOccurrence>? _allowedFaults;
    private readonly List<FaultOccurrence> _appliedFaults = new();

    public FaultExecutionController(SimulationReproduction? reproduction)
    {
        _allowedFaults = reproduction is null ? null : new HashSet<FaultOccurrence>(reproduction.Faults);
    }

    public IReadOnlyList<FaultOccurrence> AppliedFaults => _appliedFaults.AsReadOnly();

    public bool ShouldApply(FaultOccurrence occurrence, bool naturallyTriggered)
    {
        if (!naturallyTriggered)
        {
            return false;
        }

        if (_allowedFaults is not null && !_allowedFaults.Contains(occurrence))
        {
            return false;
        }

        _appliedFaults.Add(occurrence);
        return true;
    }
}
