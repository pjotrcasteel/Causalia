using Causalia.FailureIntelligence;

namespace Causalia.Visualization;

internal sealed class TraceHtmlSummary
{
    public ulong Seed { get; init; }

    public int Steps { get; init; }

    public long VirtualElapsedTicks { get; init; }

    public int EventCount { get; init; }

    public int FaultCount { get; init; }

    public int TimeTravelCheckpointCount { get; init; }

    public long DroppedTimeTravelCheckpointCount { get; init; }

    public string ReplayToken { get; init; } = string.Empty;

    public string? FailureType { get; init; }

    public string? FailureMessage { get; init; }
    public string? FailureSignature { get; init; }

    public FailureKind? FailureKind { get; init; }

    public FailureTrigger? FailureTrigger { get; init; }

    public FailureAnalysisConfidence? FailureConfidence { get; init; }

    public string? ReproductionToken { get; init; }

}
