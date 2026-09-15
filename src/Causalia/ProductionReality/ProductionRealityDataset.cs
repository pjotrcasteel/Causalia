using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Causalia.Internal;

namespace Causalia.ProductionReality;

/// <summary>
/// Contains normalized production observations that can be profiled or replayed inside Causalia.
/// </summary>
public sealed class ProductionRealityDataset
{
    private const int MaximumJsonCharacters = 67_108_864;
    private const int MaximumObservations = 1_000_000;
    private const string LegacySchemaVersion = "pr1";
    private const string SchemaVersion = "pr2";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private ProductionRealityDataset(string source, IReadOnlyList<ProductionRealityObservation> observations)
    {
        Source = source;
        Observations = observations;
        Fingerprint = CreateFingerprint(source, observations);
    }

    /// <summary>
    /// Gets the human-readable telemetry source name.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the stable content fingerprint for this normalized dataset.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    /// Gets normalized observations ordered by start time and identifier.
    /// </summary>
    public IReadOnlyList<ProductionRealityObservation> Observations { get; }

    /// <summary>
    /// Creates a normalized dataset from application-defined production observations.
    /// </summary>
    public static ProductionRealityDataset Create(string source, IEnumerable<ProductionRealityObservation> observations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(observations);
        var normalized = observations
            .Select(Normalize)
            .Take(MaximumObservations + 1)
            .OrderBy(value => value.StartedAt)
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one production observation is required.", nameof(observations));
        }

        if (normalized.Length > MaximumObservations)
        {
            throw new ArgumentException($"At most {MaximumObservations} production observations are supported.", nameof(observations));
        }

        if (normalized.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new ArgumentException("Production observation identifiers must be unique.", nameof(observations));
        }

        return new ProductionRealityDataset(source, normalized);
    }

    /// <summary>
    /// Creates a normalized dataset from stopped .NET activities without requiring an OpenTelemetry package dependency.
    /// </summary>
    public static ProductionRealityDataset FromActivities(string source, IEnumerable<Activity> activities)
    {
        ArgumentNullException.ThrowIfNull(activities);
        return Create(source, activities.Select(FromActivity));
    }

    /// <summary>
    /// Parses the portable pr2 or legacy pr1 JSON representation emitted by <see cref="ToJson"/>.
    /// </summary>
    public static ProductionRealityDataset ParseJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        if (json.Length > MaximumJsonCharacters)
        {
            throw new ArgumentException($"Production reality JSON cannot exceed {MaximumJsonCharacters} characters.", nameof(json));
        }

        var document = JsonSerializer.Deserialize<DatasetDocument>(json, JsonOptions)
            ?? throw new FormatException("Production reality JSON did not contain a dataset.");

        if (!string.Equals(document.SchemaVersion, SchemaVersion, StringComparison.Ordinal) &&
            !string.Equals(document.SchemaVersion, LegacySchemaVersion, StringComparison.Ordinal))
        {
            throw new FormatException(
                $"Unsupported production reality schema '{document.SchemaVersion}'. Expected '{SchemaVersion}' or '{LegacySchemaVersion}'.");
        }

        return Create(
            document.Source,
            document.Observations.Select(
                value => new ProductionRealityObservation
                {
                    Id = value.Id,
                    Operation = value.Operation,
                    StartedAt = value.StartedAt,
                    Duration = TimeSpan.FromTicks(value.DurationTicks),
                    Outcome = value.Outcome,
                    CorrelationId = value.CorrelationId,
                    Attributes = value.Attributes
                }));
    }

    /// <summary>
    /// Serializes this dataset to the dependency-free portable pr2 JSON representation.
    /// </summary>
    public string ToJson()
    {
        var document = new DatasetDocument
        {
            SchemaVersion = SchemaVersion,
            Source = Source,
            Observations = Observations.Select(ToDocument).ToArray()
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <summary>
    /// Derives deterministic timing and outcome distributions from the captured production evidence.
    /// </summary>
    public ProductionRealityProfile CreateProfile()
    {
        var profiles = Observations
            .GroupBy(value => value.Operation, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new ProductionRealityOperationProfile(group.Key, group.ToArray()),
                StringComparer.Ordinal);
        return new ProductionRealityProfile(Fingerprint, profiles);
    }

    /// <summary>
    /// Returns one correlated production flow for exact relative-timing replay.
    /// </summary>
    public ProductionRealityIncident GetIncident(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var observations = Observations
            .Where(value => string.Equals(value.CorrelationId, correlationId, StringComparison.Ordinal))
            .OrderBy(value => value.StartedAt)
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();

        return observations.Length == 0
            ? throw new KeyNotFoundException($"Production reality does not contain correlation '{correlationId}'.")
            : new ProductionRealityIncident(correlationId, Fingerprint, observations);
    }

    private static ProductionRealityObservation FromActivity(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        var id = activity.Id ?? $"{activity.TraceId}:{activity.SpanId}";
        var operation = string.IsNullOrWhiteSpace(activity.DisplayName) ? activity.OperationName : activity.DisplayName;
        var attributes = activity.TagObjects
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                StringComparer.Ordinal);

        return new ProductionRealityObservation
        {
            Id = id,
            Operation = operation,
            StartedAt = new DateTimeOffset(activity.StartTimeUtc, TimeSpan.Zero),
            Duration = activity.Duration,
            Outcome = activity.Status == ActivityStatusCode.Error ? ProductionRealityOutcome.Failure : ProductionRealityOutcome.Success,
            CorrelationId = activity.TraceId == default ? null : activity.TraceId.ToString(),
            Attributes = attributes
        };
    }

    private static ProductionRealityObservation Normalize(ProductionRealityObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(observation.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(observation.Operation);

        if (observation.Duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(observation), observation.Duration, "Production duration cannot be negative.");
        }

        var attributes = observation.Attributes
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
        return new ProductionRealityObservation
        {
            Id = observation.Id,
            Operation = observation.Operation,
            StartedAt = observation.StartedAt,
            Duration = observation.Duration,
            Outcome = observation.Outcome,
            CorrelationId = observation.CorrelationId,
            Attributes = attributes
        };
    }

    private static ObservationDocument ToDocument(ProductionRealityObservation observation)
    {
        return new ObservationDocument
        {
            Id = observation.Id,
            Operation = observation.Operation,
            StartedAt = observation.StartedAt,
            DurationTicks = observation.Duration.Ticks,
            Outcome = observation.Outcome,
            CorrelationId = observation.CorrelationId,
            Attributes = observation.Attributes.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal)
        };
    }

    private static string CreateFingerprint(string source, IReadOnlyList<ProductionRealityObservation> observations)
    {
        var builder = new StringBuilder();
        CanonicalString.AppendTo(builder, SchemaVersion);
        CanonicalString.AppendTo(builder, source);
        CanonicalString.AppendTo(builder, observations.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var observation in observations)
        {
            CanonicalString.AppendTo(builder, observation.Id);
            CanonicalString.AppendTo(builder, observation.Operation);
            CanonicalString.AppendTo(builder, observation.StartedAt.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
            CanonicalString.AppendTo(builder, observation.Duration.Ticks.ToString(CultureInfo.InvariantCulture));
            CanonicalString.AppendTo(builder, ((int)observation.Outcome).ToString(CultureInfo.InvariantCulture));
            CanonicalString.AppendTo(builder, observation.CorrelationId);
            CanonicalString.AppendTo(builder, observation.Attributes.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var attribute in observation.Attributes.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                CanonicalString.AppendTo(builder, attribute.Key);
                CanonicalString.AppendTo(builder, attribute.Value);
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"pr2:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class DatasetDocument
    {
        public string SchemaVersion { get; init; } = ProductionRealityDataset.SchemaVersion;

        public required string Source { get; init; }

        public required IReadOnlyList<ObservationDocument> Observations { get; init; }
    }

    private sealed class ObservationDocument
    {
        public required string Id { get; init; }

        public required string Operation { get; init; }

        public required DateTimeOffset StartedAt { get; init; }

        public required long DurationTicks { get; init; }

        public required ProductionRealityOutcome Outcome { get; init; }

        public string? CorrelationId { get; init; }

        public Dictionary<string, string> Attributes { get; init; } = new(StringComparer.Ordinal);
    }
}
