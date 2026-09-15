namespace Causalia.Visualization;

/// <summary>
/// Configures self-contained HTML trace export.
/// </summary>
public sealed class TraceHtmlOptions
{
    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string Title { get; set; } = "Causalia Trace";

    /// <summary>
    /// Gets or sets whether scheduler events are initially visible.
    /// </summary>
    public bool ShowSchedulerEvents { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of events embedded in one HTML document.
    /// </summary>
    public int MaxEvents { get; set; } = 50_000;
}
