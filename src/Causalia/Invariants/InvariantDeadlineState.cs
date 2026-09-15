namespace Causalia.Invariants;

internal sealed class InvariantDeadlineState
{
    public InvariantDeadlineState(SimulationInvariants invariants, InvariantRegistration registration)
    {
        Invariants = invariants;
        Registration = registration;
    }

    public SimulationInvariants Invariants { get; }

    public InvariantRegistration Registration { get; }
}
