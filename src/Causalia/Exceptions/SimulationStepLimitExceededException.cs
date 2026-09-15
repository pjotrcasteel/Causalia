namespace Causalia.Exceptions;

/// <summary>
/// Indicates that a simulation exceeded its configured deterministic scheduler step limit.
/// </summary>
public sealed class SimulationStepLimitExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationStepLimitExceededException"/> class.
    /// </summary>
    public SimulationStepLimitExceededException(int maximumSteps)
        : base($"The simulation exceeded the configured maximum of {maximumSteps} scheduler steps.")
    {
        MaximumSteps = maximumSteps;
    }

    /// <summary>
    /// Gets the configured maximum number of scheduler steps.
    /// </summary>
    public int MaximumSteps { get; }
}
