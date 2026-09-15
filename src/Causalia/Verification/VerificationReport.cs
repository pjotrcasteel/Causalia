using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Causalia.FailureIntelligence;

namespace Causalia.Verification;

/// <summary>
/// Portable deterministic verification artifact designed for CI storage, comparison and triage.
/// </summary>
public sealed class VerificationReport
{
    private const int MaximumJsonCharacters = 67_108_864;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    internal VerificationReport(string planName, IReadOnlyList<VerificationReportStep> steps, IReadOnlyList<VerificationReportFailure> failures)
    {
        PlanName = planName;
        Steps = steps;
        Failures = failures;
        Fingerprint = ComputeFingerprint(planName, steps, failures);
    }

    private VerificationReport(VerificationReportPayload payload)
    {
        PlanName = payload.PlanName;
        Steps = payload.Steps.Select(CreateStep).ToList().AsReadOnly();
        Failures = payload.Failures.Select(CreateFailure).ToList().AsReadOnly();
        Fingerprint = payload.Fingerprint;
        var expected = ComputeFingerprint(PlanName, Steps, Failures);

        if (!string.Equals(Fingerprint, expected, StringComparison.Ordinal))
        {
            throw new FormatException("Verification report fingerprint does not match its deterministic content.");
        }
    }

    /// <summary>
    /// Gets the portable report schema version.
    /// </summary>
    public string SchemaVersion => "vp1";

    /// <summary>
    /// Gets the stable verification-plan name.
    /// </summary>
    public string PlanName { get; }

    /// <summary>
    /// Gets the content-addressed vp1 fingerprint for this verification outcome.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    /// Gets portable report data for every plan step in deterministic plan order.
    /// </summary>
    public IReadOnlyList<VerificationReportStep> Steps { get; }

    /// <summary>
    /// Gets semantic failure clusters in deterministic engineering triage order.
    /// </summary>
    public IReadOnlyList<VerificationReportFailure> Failures { get; }

    /// <summary>
    /// Gets whether every verification step passed.
    /// </summary>
    public bool Passed => Steps.All(value => value.Status == VerificationStepStatus.Passed);

    /// <summary>
    /// Serializes this report to portable deterministic JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(CreatePayload(), JsonOptions);
    }

    /// <summary>
    /// Parses and validates a portable vp1 verification report.
    /// </summary>
    public static VerificationReport ParseJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        if (json.Length > MaximumJsonCharacters)
        {
            throw new ArgumentException($"Verification report JSON cannot exceed {MaximumJsonCharacters} characters.", nameof(json));
        }

        var payload = JsonSerializer.Deserialize<VerificationReportPayload>(json, JsonOptions)
            ?? throw new FormatException("Verification report JSON did not contain an object.");

        if (!string.Equals(payload.SchemaVersion, "vp1", StringComparison.Ordinal))
        {
            throw new FormatException($"Unsupported verification report schema '{payload.SchemaVersion}'.");
        }

        return new VerificationReport(payload);
    }

    internal static VerificationReport Create(
        string planName,
        IReadOnlyList<VerificationStepResult> steps,
        FailureIntelligenceReport failureIntelligence)
    {
        var reportSteps = steps.Select(CreateStep).ToList().AsReadOnly();
        var failures = failureIntelligence.Clusters.Select(CreateFailure).ToList().AsReadOnly();
        return new VerificationReport(planName, reportSteps, failures);
    }

    private VerificationReportPayload CreatePayload()
    {
        return new VerificationReportPayload
        {
            SchemaVersion = SchemaVersion,
            PlanName = PlanName,
            Fingerprint = Fingerprint,
            Steps = Steps.Select(CreatePayload).ToArray(),
            Failures = Failures.Select(CreatePayload).ToArray()
        };
    }

    private static VerificationReportStep CreateStep(VerificationStepResult result)
    {
        var fingerprints = result.ProductionReality?.SourceFingerprints ?? Array.Empty<string>();
        return new VerificationReportStep(
            new VerificationReportStepData
            {
                Name = result.Name,
                Kind = result.Kind,
                Status = result.Status,
                Seed = result.Seed,
                Summary = CreateSummary(result),
                FailureSignature = result.Failure?.Signature.Token,
                ScheduleReplayToken = result.Simulation?.Schedule.ReplayToken ?? result.Failure?.Schedule.ReplayToken,
                TimeTravelCheckpointCount = result.TimeTravel?.Checkpoints.Count ?? 0,
                ProductionRealityFingerprints = fingerprints.ToArray()
            });
    }

    private static VerificationReportFailure CreateFailure(FailureCluster cluster)
    {
        return new VerificationReportFailure(
            new VerificationReportFailureData
            {
                TriageRank = cluster.TriageRank,
                Signature = cluster.Signature.Token,
                Kind = cluster.Kind.ToString(),
                OccurrenceCount = cluster.OccurrenceCount,
                Summary = cluster.Representative.Summary,
                Trigger = cluster.Representative.Trigger.ToString(),
                ScheduleReplayToken = cluster.Representative.ScheduleReplayToken,
                ReproductionToken = cluster.Representative.ReproductionToken
            });
    }

    private static VerificationReportStep CreateStep(VerificationReportStepPayload payload)
    {
        return new VerificationReportStep(
            new VerificationReportStepData
            {
                Name = payload.Name,
                Kind = payload.Kind,
                Status = payload.Status,
                Seed = payload.Seed,
                Summary = payload.Summary,
                FailureSignature = payload.FailureSignature,
                ScheduleReplayToken = payload.ScheduleReplayToken,
                TimeTravelCheckpointCount = payload.TimeTravelCheckpointCount,
                ProductionRealityFingerprints = payload.ProductionRealityFingerprints
            });
    }

    private static VerificationReportFailure CreateFailure(VerificationReportFailurePayload payload)
    {
        return new VerificationReportFailure(
            new VerificationReportFailureData
            {
                TriageRank = payload.TriageRank,
                Signature = payload.Signature,
                Kind = payload.Kind,
                OccurrenceCount = payload.OccurrenceCount,
                Summary = payload.Summary,
                Trigger = payload.Trigger,
                ScheduleReplayToken = payload.ScheduleReplayToken,
                ReproductionToken = payload.ReproductionToken
            });
    }

    private static VerificationReportStepPayload CreatePayload(VerificationReportStep step)
    {
        return new VerificationReportStepPayload
        {
            Name = step.Name,
            Kind = step.Kind,
            Status = step.Status,
            Seed = step.Seed,
            Summary = step.Summary,
            FailureSignature = step.FailureSignature,
            ScheduleReplayToken = step.ScheduleReplayToken,
            TimeTravelCheckpointCount = step.TimeTravelCheckpointCount,
            ProductionRealityFingerprints = step.ProductionRealityFingerprints.ToArray()
        };
    }

    private static VerificationReportFailurePayload CreatePayload(VerificationReportFailure failure)
    {
        return new VerificationReportFailurePayload
        {
            TriageRank = failure.TriageRank,
            Signature = failure.Signature,
            Kind = failure.Kind,
            OccurrenceCount = failure.OccurrenceCount,
            Summary = failure.Summary,
            Trigger = failure.Trigger,
            ScheduleReplayToken = failure.ScheduleReplayToken,
            ReproductionToken = failure.ReproductionToken
        };
    }

    private static string CreateSummary(VerificationStepResult result)
    {
        if (result.Status == VerificationStepStatus.Skipped)
        {
            return result.SkipReason ?? "Skipped.";
        }

        if (result.FailureAnalysis is not null)
        {
            return result.FailureAnalysis.Summary;
        }

        if (result.Simulation is not null)
        {
            return $"Completed {result.Simulation.Steps} scheduler steps in {result.Simulation.VirtualElapsed}.";
        }

        if (result.Exploration is not null)
        {
            return $"Explored {result.Exploration.SchedulesExplored} schedules using {result.Exploration.Strategy}.";
        }

        if (result.ModelBased is not null)
        {
            return $"Explored {result.ModelBased.SequencesExplored} model sequences across {result.ModelBased.SchedulesExplored} schedules.";
        }

        return "Completed.";
    }

    private static string ComputeFingerprint(
        string planName,
        IReadOnlyList<VerificationReportStep> steps,
        IReadOnlyList<VerificationReportFailure> failures)
    {
        var canonical = new StringBuilder();
        Append(canonical, planName);
        canonical.Append(steps.Count).Append('|');

        foreach (var step in steps)
        {
            Append(canonical, step.Name);
            canonical.Append((int)step.Kind).Append('|');
            canonical.Append((int)step.Status).Append('|');
            canonical.Append(step.Seed).Append('|');
            Append(canonical, step.Summary);
            Append(canonical, step.FailureSignature);
            Append(canonical, step.ScheduleReplayToken);
            canonical.Append(step.TimeTravelCheckpointCount).Append('|');
            canonical.Append(step.ProductionRealityFingerprints.Count).Append('|');

            foreach (var fingerprint in step.ProductionRealityFingerprints)
            {
                Append(canonical, fingerprint);
            }
        }

        canonical.Append(failures.Count).Append('|');
        foreach (var failure in failures)
        {
            canonical.Append(failure.TriageRank).Append('|');
            Append(canonical, failure.Signature);
            Append(canonical, failure.Kind);
            canonical.Append(failure.OccurrenceCount).Append('|');
            Append(canonical, failure.Summary);
            Append(canonical, failure.Trigger);
            Append(canonical, failure.ScheduleReplayToken);
            Append(canonical, failure.ReproductionToken);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"vp1:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static void Append(StringBuilder builder, string? value)
    {
        value ??= string.Empty;
        builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class VerificationReportPayload
    {
        public string SchemaVersion { get; set; } = string.Empty;

        public string PlanName { get; set; } = string.Empty;

        public string Fingerprint { get; set; } = string.Empty;

        public VerificationReportStepPayload[] Steps { get; set; } = [];

        public VerificationReportFailurePayload[] Failures { get; set; } = [];
    }

    private sealed class VerificationReportStepPayload
    {
        public string Name { get; set; } = string.Empty;

        public VerificationStepKind Kind { get; set; }

        public VerificationStepStatus Status { get; set; }

        public ulong Seed { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string? FailureSignature { get; set; }

        public string? ScheduleReplayToken { get; set; }

        public int TimeTravelCheckpointCount { get; set; }

        public string[] ProductionRealityFingerprints { get; set; } = [];
    }

    private sealed class VerificationReportFailurePayload
    {
        public int TriageRank { get; set; }

        public string Signature { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public int OccurrenceCount { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string Trigger { get; set; } = string.Empty;

        public string ScheduleReplayToken { get; set; } = string.Empty;

        public string? ReproductionToken { get; set; }
    }
}
