using Causalia.Exceptions;
using Causalia.FailureIntelligence;

namespace Causalia.Visualization;

/// <summary>
/// Provides convenient visualization helpers for Causalia results and failures.
/// </summary>
public static class SimulationTraceVisualizationExtensions
{
    /// <summary>
    /// Creates a normalized visual trace document.
    /// </summary>
    public static SimulationTraceDocument ToTraceDocument(this SimulationResult result)
    {
        return SimulationTraceVisualizer.CreateDocument(result);
    }

    /// <summary>
    /// Creates a normalized visual trace document.
    /// </summary>
    public static SimulationTraceDocument ToTraceDocument(this SimulationFailedException failure)
    {
        return SimulationTraceVisualizer.CreateDocument(failure);
    }

    /// <summary>
    /// Creates a normalized visual trace document enriched with deterministic failure intelligence.
    /// </summary>
    public static SimulationTraceDocument ToTraceDocument(this FailureAnalysis analysis)
    {
        return SimulationTraceVisualizer.CreateDocument(analysis);
    }

    /// <summary>
    /// Creates a normalized visual trace document.
    /// </summary>
    public static SimulationTraceDocument ToTraceDocument(this SimulationExplorationFailedException failure)
    {
        return SimulationTraceVisualizer.CreateDocument(failure);
    }

    /// <summary>
    /// Creates a self-contained interactive HTML trace viewer.
    /// </summary>
    public static string ToTraceHtml(this SimulationResult result, TraceHtmlOptions? options = null)
    {
        return SimulationTraceHtmlExporter.Export(result.ToTraceDocument(), options);
    }

    /// <summary>
    /// Creates a self-contained interactive HTML trace viewer.
    /// </summary>
    public static string ToTraceHtml(this SimulationFailedException failure, TraceHtmlOptions? options = null)
    {
        return SimulationTraceHtmlExporter.Export(failure.ToTraceDocument(), options);
    }

    /// <summary>
    /// Creates a self-contained interactive HTML trace viewer enriched with deterministic failure intelligence.
    /// </summary>
    public static string ToTraceHtml(this FailureAnalysis analysis, TraceHtmlOptions? options = null)
    {
        return SimulationTraceHtmlExporter.Export(analysis.ToTraceDocument(), options);
    }

    /// <summary>
    /// Creates a self-contained interactive HTML trace viewer.
    /// </summary>
    public static string ToTraceHtml(this SimulationExplorationFailedException failure, TraceHtmlOptions? options = null)
    {
        return SimulationTraceHtmlExporter.Export(failure.ToTraceDocument(), options);
    }
}
