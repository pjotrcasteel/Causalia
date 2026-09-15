namespace Causalia.Faults;

/// <summary>
/// Identifies one deterministic fault-policy occurrence that was applied during a simulation run.
/// </summary>
public sealed record FaultOccurrence
{
    /// <summary>
    /// Initializes a new fault occurrence identifier.
    /// </summary>
    public FaultOccurrence(string scope, long injectorId, long occurrence, string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        if (injectorId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(injectorId), injectorId, "InjectorId must be greater than zero.");
        }

        if (occurrence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrence), occurrence, "Occurrence must be greater than zero.");
        }

        Scope = scope;
        InjectorId = injectorId;
        Occurrence = occurrence;
        PolicyName = policyName;
    }

    /// <summary>
    /// Gets the logical fault-injector scope.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// Gets the deterministic fault-injector instance identifier assigned by the simulation context.
    /// </summary>
    public long InjectorId { get; }

    /// <summary>
    /// Gets the one-based evaluation occurrence within the fault injector.
    /// </summary>
    public long Occurrence { get; }

    /// <summary>
    /// Gets the stable policy name.
    /// </summary>
    public string PolicyName { get; }
}
