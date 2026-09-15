using System.Text.Json;

namespace Causalia.ModelBased;

/// <summary>
/// Identifies one deterministic state-machine command sequence and provides a stable replay token.
/// </summary>
public sealed class ModelSequence
{
    private const int MaximumTokenLength = 1_048_576;
    private const string Prefix = "mb1:";

    /// <summary>
    /// Initializes a model sequence from stable command names.
    /// </summary>
    public ModelSequence(IReadOnlyList<string> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var copy = new List<string>(commands.Count);

        foreach (var command in commands)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            copy.Add(command);
        }

        Commands = copy.AsReadOnly();
        ReplayToken = CreateReplayToken(Commands);
    }

    /// <summary>
    /// Gets the ordered stable command names in this sequence.
    /// </summary>
    public IReadOnlyList<string> Commands { get; }

    /// <summary>
    /// Gets the portable model-sequence replay token.
    /// </summary>
    public string ReplayToken { get; }

    /// <summary>
    /// Parses a model-sequence replay token.
    /// </summary>
    public static ModelSequence Parse(string replayToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayToken);

        if (replayToken.Length > MaximumTokenLength)
        {
            throw new ArgumentException($"Model replay tokens cannot exceed {MaximumTokenLength} characters.", nameof(replayToken));
        }

        if (!replayToken.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"Model replay tokens must start with '{Prefix}'.");
        }

        try
        {
            var payload = replayToken[Prefix.Length..];
            var bytes = FromBase64Url(payload);
            var commands = JsonSerializer.Deserialize<List<string>>(bytes)
                ?? throw new FormatException("The model replay token contains no command sequence.");
            return new ModelSequence(commands);
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new FormatException("The model replay token is malformed.", exception);
        }
    }

    internal ModelSequence Append(string command)
    {
        var commands = new List<string>(Commands.Count + 1);
        commands.AddRange(Commands);
        commands.Add(command);
        return new ModelSequence(commands);
    }

    internal ModelSequence RemoveRange(int index, int count)
    {
        var commands = new List<string>(Commands.Count - count);
        commands.AddRange(Commands.Take(index));
        commands.AddRange(Commands.Skip(index + count));
        return new ModelSequence(commands);
    }

    private static string CreateReplayToken(IReadOnlyList<string> commands)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(commands);
        return Prefix + ToBase64Url(bytes);
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] FromBase64Url(string payload)
    {
        var base64 = payload.Replace('-', '+').Replace('_', '/');
        var padding = base64.Length % 4;
        if (padding != 0)
        {
            base64 = base64.PadRight(base64.Length + 4 - padding, '=');
        }

        return Convert.FromBase64String(base64);
    }
}
