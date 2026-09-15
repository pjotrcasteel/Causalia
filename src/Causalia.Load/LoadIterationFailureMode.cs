namespace Causalia.Load;

/// <summary>
/// Controls how an iteration exception affects a deterministic load run.
/// </summary>
public enum LoadIterationFailureMode
{
    /// <summary>
    /// Stops the simulation immediately when an iteration fails.
    /// </summary>
    StopOnFirstFailure,

    /// <summary>
    /// Records the failed iteration and continues the load run.
    /// </summary>
    RecordAndContinue
}
