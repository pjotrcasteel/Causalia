namespace Causalia.TimeTravel;

/// <summary>
/// Contains one immutable textual state observation captured at a checkpoint.
/// </summary>
public sealed class TimeTravelProbeSnapshot
{
    internal TimeTravelProbeSnapshot(string name, string? value, string? errorType, string? errorMessage)
    {
        Name = name;
        Value = value;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the stable probe name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the captured deterministic representation when the probe succeeded.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Gets the probe exception type when capture failed.
    /// </summary>
    public string? ErrorType { get; }

    /// <summary>
    /// Gets the probe exception message when capture failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets whether the state probe completed successfully.
    /// </summary>
    public bool Succeeded => ErrorType is null;
}
