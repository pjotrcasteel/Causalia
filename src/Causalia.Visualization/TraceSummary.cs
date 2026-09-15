using Causalia.FailureIntelligence;

namespace Causalia.Visualization;

/// <summary>
/// Summarizes one visualized simulation execution.
/// </summary>
public sealed class TraceSummary
{
    internal TraceSummary()
    {
    }

    /// <summary>
    /// Gets the simulation seed.
    /// </summary>
    public ulong Seed { get; internal init; }

    /// <summary>
    /// Gets the executed scheduler-step count when known.
    /// </summary>
    public int Steps { get; internal init; }

    /// <summary>
    /// Gets the elapsed virtual time represented by the trace.
    /// </summary>
    public TimeSpan VirtualElapsed { get; internal init; }

    /// <summary>
    /// Gets the number of visualized trace events.
    /// </summary>
    public int EventCount { get; internal init; }

    /// <summary>
    /// Gets the number of applied deterministic faults.
    /// </summary>
    public int FaultCount { get; internal init; }

    /// <summary>
    /// Gets the number of retained time-travel checkpoints.
    /// </summary>
    public int TimeTravelCheckpointCount { get; internal init; }

    /// <summary>
    /// Gets how many older time-travel checkpoints were discarded by retention.
    /// </summary>
    public long DroppedTimeTravelCheckpointCount { get; internal init; }

    /// <summary>
    /// Gets the number of production observations consumed by the deterministic execution.
    /// </summary>
    public int ProductionRealityApplicationCount { get; internal init; }

    /// <summary>
    /// Gets the number of distinct production datasets referenced by the execution.
    /// </summary>
    public int ProductionRealitySourceCount { get; internal init; }

    /// <summary>
    /// Gets the exact schedule replay token.
    /// </summary>
    public string ReplayToken { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the failure type for a failed execution, otherwise <see langword="null"/>.
    /// </summary>
    public string? FailureType { get; internal init; }

    /// <summary>
    /// Gets the failure message for a failed execution, otherwise <see langword="null"/>.
    /// </summary>
    public string? FailureMessage { get; internal init; }
    /// <summary>
    /// Gets the stable semantic failure signature for a failed execution.
    /// </summary>
    public string? FailureSignature { get; internal init; }

    /// <summary>
    /// Gets the semantic failure classification for a failed execution.
    /// </summary>
    public FailureKind? FailureKind { get; internal init; }

    /// <summary>
    /// Gets the deterministically proven trigger attribution when a failure analysis was supplied.
    /// </summary>
    public FailureTrigger? FailureTrigger { get; internal init; }

    /// <summary>
    /// Gets the failure-analysis confidence when deterministic reduction was supplied.
    /// </summary>
    public FailureAnalysisConfidence? FailureConfidence { get; internal init; }

    /// <summary>
    /// Gets the compact minimized reproduction token when one is available.
    /// </summary>
    public string? ReproductionToken { get; internal init; }

}
