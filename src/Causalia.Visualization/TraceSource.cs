using Causalia.FailureIntelligence;
using Causalia.Tracing;
using Causalia.TimeTravel;
using Causalia.ProductionReality;

namespace Causalia.Visualization;

internal sealed class TraceSource
{
    public ulong Seed { get; init; }

    public int Steps { get; init; }

    public TimeSpan VirtualElapsed { get; init; }

    public string ReplayToken { get; init; } = string.Empty;

    public IReadOnlyList<SimulationTraceEntry> Trace { get; init; } = Array.Empty<SimulationTraceEntry>();

    public int FaultCount { get; init; }

    public Exception? Failure { get; init; }

    public FailureAnalysis? FailureAnalysis { get; init; }

    public string? FailureSignature { get; init; }

    public FailureKind? FailureKind { get; init; }

    public TimeTravelTimeline? TimeTravel { get; init; }

    public ProductionRealityEvidence? ProductionReality { get; init; }
}
