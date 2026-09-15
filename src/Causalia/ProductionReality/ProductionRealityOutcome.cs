namespace Causalia.ProductionReality;

/// <summary>
/// Classifies the observed outcome of one production operation.
/// </summary>
public enum ProductionRealityOutcome
{
    /// <summary>
    /// The production operation completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The production operation completed with a failure.
    /// </summary>
    Failure,

    /// <summary>
    /// The production operation exceeded its effective deadline.
    /// </summary>
    Timeout,

    /// <summary>
    /// The production operation was cancelled before successful completion.
    /// </summary>
    Cancelled
}
