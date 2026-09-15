namespace Causalia.Visualization;

/// <summary>
/// Contains one watched state value prepared for debugger visualization.
/// </summary>
public sealed class VisualTimeTravelProbe
{
    internal VisualTimeTravelProbe()
    {
    }

    /// <summary>
    /// Gets the stable probe name.
    /// </summary>
    public string Name { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the captured deterministic representation when capture succeeded.
    /// </summary>
    public string? Value { get; internal init; }

    /// <summary>
    /// Gets whether the probe capture succeeded.
    /// </summary>
    public bool Succeeded { get; internal init; }

    /// <summary>
    /// Gets the exception type when the probe failed to capture.
    /// </summary>
    public string? ErrorType { get; internal init; }

    /// <summary>
    /// Gets the exception message when the probe failed to capture.
    /// </summary>
    public string? ErrorMessage { get; internal init; }
}
