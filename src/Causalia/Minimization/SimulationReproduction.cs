using System.Text.Json;
using Causalia.Faults;

namespace Causalia.Minimization;

/// <summary>
/// Describes a compact deterministic reproduction containing only forced scheduler choices and allowed fault occurrences.
/// </summary>
public sealed class SimulationReproduction
{
    private const int MaximumTokenLength = 1_048_576;
    private const string TokenPrefix = "m1:";
    private readonly Dictionary<int, int> _schedulerChoices;

    /// <summary>
    /// Initializes a deterministic reproduction.
    /// </summary>
    public SimulationReproduction(
        ulong seed,
        IReadOnlyList<SchedulerChoice> schedulerChoices,
        IReadOnlyList<FaultOccurrence> faults)
    {
        ArgumentNullException.ThrowIfNull(schedulerChoices);
        ArgumentNullException.ThrowIfNull(faults);
        ValidateSchedulerChoices(schedulerChoices);
        ValidateFaults(faults);

        Seed = seed;
        SchedulerChoices = schedulerChoices.ToList().AsReadOnly();
        Faults = faults.ToList().AsReadOnly();
        _schedulerChoices = SchedulerChoices.ToDictionary(choice => choice.DecisionIndex, choice => choice.SelectedIndex);
    }

    /// <summary>
    /// Gets the seed used by the deterministic random and fault streams.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the non-canonical scheduler choices that must be forced.
    /// Missing decisions use the canonical candidate at index zero.
    /// </summary>
    public IReadOnlyList<SchedulerChoice> SchedulerChoices { get; }

    /// <summary>
    /// Gets the fault occurrences that are allowed to take effect.
    /// Naturally triggered faults not listed here are suppressed.
    /// </summary>
    public IReadOnlyList<FaultOccurrence> Faults { get; }

    /// <summary>
    /// Gets a compact, versioned token that can be logged and parsed later.
    /// </summary>
    public string ReplayToken => ToString();

    /// <summary>
    /// Parses a reproduction token previously produced by <see cref="ReplayToken"/>.
    /// </summary>
    public static SimulationReproduction Parse(string replayToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayToken);

        if (replayToken.Length > MaximumTokenLength)
        {
            throw new ArgumentException($"Minimization replay tokens cannot exceed {MaximumTokenLength} characters.", nameof(replayToken));
        }

        if (!replayToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            throw new FormatException("The minimization replay token has an unsupported format or version.");
        }

        try
        {
            var bytes = DecodeBase64Url(replayToken[TokenPrefix.Length..]);
            var payload = JsonSerializer.Deserialize<ReproductionPayload>(bytes)
                ?? throw new FormatException("The minimization replay token contains an empty payload.");

            if (payload.SchedulerChoices is null || payload.Faults is null)
            {
                throw new FormatException("The minimization replay token is missing required collections.");
            }

            var choices = payload.SchedulerChoices
                .Select(choice => new SchedulerChoice(choice.DecisionIndex, choice.SelectedIndex))
                .ToList()
                .AsReadOnly();
            var faults = payload.Faults
                .Select(fault => new FaultOccurrence(fault.Scope, fault.InjectorId, fault.Occurrence, fault.PolicyName))
                .ToList()
                .AsReadOnly();
            return new SimulationReproduction(payload.Seed, choices, faults);
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new FormatException("The minimization replay token contains an invalid payload.", exception);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var payload = new ReproductionPayload(
            Seed,
            SchedulerChoices.Select(choice => new SchedulerChoicePayload(choice.DecisionIndex, choice.SelectedIndex)).ToArray(),
            Faults.Select(fault => new FaultPayload(fault.Scope, fault.InjectorId, fault.Occurrence, fault.PolicyName)).ToArray());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        return TokenPrefix + EncodeBase64Url(bytes);
    }

    internal bool TryGetSchedulerChoice(int decisionIndex, out int selectedIndex)
    {
        return _schedulerChoices.TryGetValue(decisionIndex, out selectedIndex);
    }

    private static void ValidateSchedulerChoices(IReadOnlyList<SchedulerChoice> schedulerChoices)
    {
        var duplicate = schedulerChoices
            .GroupBy(choice => choice.DecisionIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Scheduler decision {duplicate.Key} is forced more than once.",
                nameof(schedulerChoices));
        }
    }

    private static void ValidateFaults(IReadOnlyList<FaultOccurrence> faults)
    {
        if (faults.Count != faults.Distinct().Count())
        {
            throw new ArgumentException("Fault occurrences must be unique.", nameof(faults));
        }
    }

    private static string EncodeBase64Url(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] DecodeBase64Url(string value)
    {
        if (value.Length == 0)
        {
            throw new FormatException("The minimization replay token contains an empty payload.");
        }

        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException("The minimization replay token contains invalid Base64Url data.")
        };
        return Convert.FromBase64String(base64);
    }

}
