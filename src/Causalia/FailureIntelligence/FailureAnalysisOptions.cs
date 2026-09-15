using Causalia.Minimization;

namespace Causalia.FailureIntelligence;

/// <summary>
/// Configures deterministic failure analysis and causal reduction.
/// </summary>
public sealed class FailureAnalysisOptions
{
    /// <summary>
    /// Gets or sets the bounded delta-debugging options used to identify essential scheduler choices and faults.
    /// </summary>
    public MinimizationOptions Minimization { get; init; } = new();

    /// <summary>
    /// Gets or sets the maximum number of final trace entries retained as human-readable failure context.
    /// </summary>
    public int TraceContextEntries { get; init; } = 25;
}
