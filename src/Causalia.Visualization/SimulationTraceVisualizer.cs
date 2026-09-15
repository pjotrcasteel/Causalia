using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.Tracing;

namespace Causalia.Visualization;

/// <summary>
/// Converts deterministic simulation results and failures into presentation-ready trace documents.
/// </summary>
public static class SimulationTraceVisualizer
{
    /// <summary>
    /// Creates a trace document for a successful simulation run.
    /// </summary>
    public static SimulationTraceDocument CreateDocument(SimulationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return CreateDocument(
            new TraceSource
            {
                Seed = result.Seed,
                Steps = result.Steps,
                VirtualElapsed = result.VirtualElapsed,
                ReplayToken = result.Schedule.ReplayToken,
                Trace = result.Trace,
                FaultCount = result.Faults.Count,
                TimeTravel = result.TimeTravel,
                ProductionReality = result.ProductionReality
            });
    }

    /// <summary>
    /// Creates a trace document for a deterministic simulation failure.
    /// </summary>
    public static SimulationTraceDocument CreateDocument(SimulationFailedException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return CreateDocument(
            new TraceSource
            {
                Seed = failure.Seed,
                Steps = failure.Trace.Count == 0 ? 0 : failure.Trace.Max(entry => entry.Step),
                VirtualElapsed = CalculateVirtualElapsed(failure.Trace),
                ReplayToken = failure.Schedule.ReplayToken,
                Trace = failure.Trace,
                FaultCount = failure.Faults.Count,
                Failure = failure.InnerException,
                FailureSignature = failure.Signature.Token,
                FailureKind = failure.Kind,
                TimeTravel = failure.TimeTravel,
                ProductionReality = failure.ProductionReality
            });
    }

    /// <summary>
    /// Creates a trace document enriched with deterministic failure intelligence.
    /// </summary>
    public static SimulationTraceDocument CreateDocument(FailureAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var failure = analysis.RepresentativeFailure;
        return CreateDocument(
            new TraceSource
            {
                Seed = failure.Seed,
                Steps = failure.Trace.Count == 0 ? 0 : failure.Trace.Max(entry => entry.Step),
                VirtualElapsed = CalculateVirtualElapsed(failure.Trace),
                ReplayToken = failure.Schedule.ReplayToken,
                Trace = failure.Trace,
                FaultCount = failure.Faults.Count,
                Failure = failure.InnerException,
                FailureAnalysis = analysis,
                FailureSignature = analysis.Signature.Token,
                FailureKind = analysis.Kind,
                TimeTravel = failure.TimeTravel,
                ProductionReality = failure.ProductionReality
            });
    }

    /// <summary>
    /// Creates a trace document for the first failure found during schedule exploration.
    /// </summary>
    public static SimulationTraceDocument CreateDocument(SimulationExplorationFailedException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return CreateDocument(failure.Failure);
    }

    private static SimulationTraceDocument CreateDocument(TraceSource source)
    {
        var events = CreateEvents(source.Trace);
        var lanes = events
            .GroupBy(traceEvent => traceEvent.Lane, StringComparer.Ordinal)
            .Select(
                group => new TraceLane
                {
                    Name = group.Key,
                    EventCount = group.Count()
                })
            .OrderByDescending(lane => lane.EventCount)
            .ThenBy(lane => lane.Name, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
        var timeTravel = CreateTimeTravel(source);
        var summary = new TraceSummary
        {
            Seed = source.Seed,
            Steps = source.Steps,
            VirtualElapsed = source.VirtualElapsed,
            EventCount = events.Count,
            FaultCount = source.FaultCount,
            TimeTravelCheckpointCount = timeTravel.Count,
            DroppedTimeTravelCheckpointCount = source.TimeTravel?.DroppedCheckpointCount ?? 0,
            ProductionRealityApplicationCount = source.ProductionReality?.Applications.Count ?? 0,
            ProductionRealitySourceCount = source.ProductionReality?.SourceFingerprints.Count ?? 0,
            ReplayToken = source.ReplayToken,
            FailureType = source.Failure?.GetType().Name,
            FailureMessage = source.Failure?.Message,
            FailureSignature = source.FailureSignature,
            FailureKind = source.FailureKind,
            FailureTrigger = source.FailureAnalysis?.Trigger,
            FailureConfidence = source.FailureAnalysis?.Confidence,
            ReproductionToken = source.FailureAnalysis?.ReproductionToken
        };
        return new SimulationTraceDocument
        {
            Summary = summary,
            Lanes = lanes,
            Events = events,
            TimeTravel = timeTravel
        };
    }

    private static IReadOnlyList<VisualTraceEvent> CreateEvents(IReadOnlyList<SimulationTraceEntry> trace)
    {
        if (trace.Count == 0)
        {
            return Array.Empty<VisualTraceEvent>();
        }

        var origin = trace[0].Timestamp;
        var events = new List<VisualTraceEvent>(trace.Count);

        for (var index = 0; index < trace.Count; index++)
        {
            var entry = trace[index];
            var classification = TraceEventClassifier.Classify(entry.Message);
            events.Add(
                new VisualTraceEvent
                {
                    Index = index,
                    Step = entry.Step,
                    Timestamp = entry.Timestamp,
                    Elapsed = entry.Timestamp - origin,
                    Message = entry.Message,
                    Category = classification.Category,
                    Severity = classification.Severity,
                    Lane = classification.Lane,
                    CorrelationId = classification.CorrelationId
                });
        }

        return events.AsReadOnly();
    }

    private static IReadOnlyList<VisualTimeTravelCheckpoint> CreateTimeTravel(TraceSource source)
    {
        if (source.TimeTravel is null || source.TimeTravel.Checkpoints.Count == 0)
        {
            return Array.Empty<VisualTimeTravelCheckpoint>();
        }

        return source.TimeTravel.Checkpoints
            .Select(
                checkpoint => new VisualTimeTravelCheckpoint
                {
                    Index = checkpoint.Index,
                    Token = checkpoint.Token,
                    Step = checkpoint.Step,
                    Timestamp = checkpoint.Timestamp,
                    Kind = checkpoint.Kind,
                    Label = checkpoint.Label,
                    TraceCount = checkpoint.TraceCount,
                    State = checkpoint.State
                        .Select(
                            state => new VisualTimeTravelProbe
                            {
                                Name = state.Name,
                                Value = state.Value,
                                Succeeded = state.Succeeded,
                                ErrorType = state.ErrorType,
                                ErrorMessage = state.ErrorMessage
                            })
                        .ToList()
                        .AsReadOnly()
                })
            .ToList()
            .AsReadOnly();
    }

    private static TimeSpan CalculateVirtualElapsed(IReadOnlyList<SimulationTraceEntry> trace)
    {
        return trace.Count < 2 ? TimeSpan.Zero : trace[^1].Timestamp - trace[0].Timestamp;
    }

}
