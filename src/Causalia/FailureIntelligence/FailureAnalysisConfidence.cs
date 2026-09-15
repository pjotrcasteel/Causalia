namespace Causalia.FailureIntelligence;

/// <summary>
/// Describes how strongly a failure analysis is supported by deterministic reproduction work.
/// </summary>
public enum FailureAnalysisConfidence
{
    /// <summary>
    /// Classification and signature were derived from the captured failure without running minimization.
    /// </summary>
    Descriptive,

    /// <summary>
    /// The failure was reproduced and reduced, but the configured minimization-attempt budget was exhausted.
    /// </summary>
    BoundedMinimization,

    /// <summary>
    /// Delta debugging completed within its configured bounds and no remaining candidate could be removed.
    /// </summary>
    Minimized
}
