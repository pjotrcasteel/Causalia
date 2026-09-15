namespace Causalia.Verification;

/// <summary>
/// Identifies the verification engine used by one platform step.
/// </summary>
public enum VerificationStepKind
{
    /// <summary>
    /// Executes one seeded deterministic simulation.
    /// </summary>
    Simulation = 0,

    /// <summary>
    /// Explores bounded scheduler interleavings.
    /// </summary>
    ScheduleExploration = 1,

    /// <summary>
    /// Explores executable model command sequences.
    /// </summary>
    ModelBased = 2
}
