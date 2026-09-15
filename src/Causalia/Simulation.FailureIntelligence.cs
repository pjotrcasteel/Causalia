using Causalia.Exceptions;
using Causalia.FailureIntelligence;
using Causalia.Runtime;

namespace Causalia;

public static partial class Simulation
{
    /// <summary>
    /// Reproduces and delta-reduces one deterministic failure to classify the controlled dimensions that remain necessary for it to occur.
    /// </summary>
    public static Task<FailureAnalysis> AnalyzeFailureAsync(
        SimulationOptions options,
        FailureAnalysisOptions analysisOptions,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        ValidateSimulationOptions(options);
        ArgumentNullException.ThrowIfNull(analysisOptions);
        ArgumentNullException.ThrowIfNull(analysisOptions.Minimization);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(scenario);

        if (analysisOptions.TraceContextEntries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(analysisOptions),
                analysisOptions.TraceContextEntries,
                "TraceContextEntries cannot be negative.");
        }

        var minimization = SimulationMinimizer.Minimize(
            options,
            analysisOptions.Minimization,
            failure,
            scenario,
            cancellationToken);
        return Task.FromResult(FailureAnalyzer.Create(failure, minimization, analysisOptions.TraceContextEntries));
    }
}
