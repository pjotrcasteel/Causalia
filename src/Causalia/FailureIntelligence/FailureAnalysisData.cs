using Causalia.Exceptions;
using Causalia.Minimization;
using Causalia.Tracing;

namespace Causalia.FailureIntelligence;

internal sealed class FailureAnalysisData
{
    public required SimulationFailedException OriginalFailure { get; init; }

    public required SimulationFailedException RepresentativeFailure { get; init; }

    public required FailureSignature Signature { get; init; }

    public required FailureTrigger Trigger { get; init; }

    public required FailureAnalysisConfidence Confidence { get; init; }

    public required string Summary { get; init; }

    public required string Explanation { get; init; }

    public MinimizationResult? Minimization { get; init; }

    public required IReadOnlyList<SimulationTraceEntry> TraceContext { get; init; }
}
