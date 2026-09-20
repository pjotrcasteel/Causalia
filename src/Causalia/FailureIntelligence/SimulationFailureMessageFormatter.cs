using System.Text;
using Causalia.Exceptions;
using Causalia.Tracing;

namespace Causalia.FailureIntelligence;

internal static class SimulationFailureMessageFormatter
{
    private const int MaximumTraceEntries = 6;

    public static string Format(SimulationFailureData data, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(failure);

        var builder = new StringBuilder();
        builder.AppendLine("CAUSALIA FAILURE");
        AppendFailureIdentity(builder, failure);
        AppendObservedOutcome(builder, failure);
        AppendSequence(builder, data.Trace);
        AppendFaultContext(builder, data, failure);
        builder.AppendLine($"Replay: {data.Schedule.ReplayToken}");
        builder.AppendLine(
            $"Reproduce: Simulation.ReplayAsync(options, SimulationSchedule.Parse(\"{data.Schedule.ReplayToken}\"), scenario, cancellationToken)");
        builder.Append($"Seed: {data.Seed}");
        return builder.ToString();
    }

    private static void AppendFailureIdentity(StringBuilder builder, Exception failure)
    {
        if (failure is SimulationInvariantException invariant)
        {
            builder.AppendLine($"Invariant: {invariant.InvariantName}");
            builder.AppendLine($"Requirement: {invariant.Kind}");
            return;
        }

        builder.AppendLine($"Failure: {failure.GetType().Name}");
    }

    private static void AppendObservedOutcome(StringBuilder builder, Exception failure)
    {
        builder.AppendLine($"Observed: {failure.Message}");

        if (failure is SimulationInvariantException)
        {
            builder.AppendLine("Why: the observed state violated a registered deterministic invariant.");
            return;
        }

        builder.AppendLine("Why: the scenario threw before the deterministic simulation completed successfully.");
    }

    private static void AppendSequence(StringBuilder builder, IReadOnlyList<SimulationTraceEntry> trace)
    {
        builder.AppendLine("Failure sequence:");
        var entries = SelectTraceEntries(trace);

        if (entries.Count == 0)
        {
            builder.AppendLine("  1. No application-level trace events were recorded before the failure.");
            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            builder.AppendLine($"  {index + 1}. [step {entry.Step}] {entry.Message}");
        }
    }

    private static IReadOnlyList<SimulationTraceEntry> SelectTraceEntries(IReadOnlyList<SimulationTraceEntry> trace)
    {
        var applicationEntries = trace
            .Where(entry => !entry.Message.StartsWith("scheduler:", StringComparison.Ordinal))
            .TakeLast(MaximumTraceEntries)
            .ToList();

        if (applicationEntries.Count > 0)
        {
            return applicationEntries.AsReadOnly();
        }

        return trace.TakeLast(MaximumTraceEntries).ToList().AsReadOnly();
    }

    private static void AppendFaultContext(StringBuilder builder, SimulationFailureData data, Exception failure)
    {
        if (data.Faults.Count == 0)
        {
            return;
        }

        var faults = string.Join(
            ", ",
            data.Faults.Take(3).Select(value => $"{value.PolicyName} ({value.Scope}, occurrence {value.Occurrence})"));
        var remainder = data.Faults.Count > 3 ? $", plus {data.Faults.Count - 3} more" : string.Empty;
        builder.AppendLine($"Faults observed: {faults}{remainder}");
        builder.AppendLine(
            "Causality: these faults were active before the failure; use failure minimization to determine which are essential.");
    }
}
