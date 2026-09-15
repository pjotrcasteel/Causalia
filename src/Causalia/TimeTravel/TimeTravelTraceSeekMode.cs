namespace Causalia.TimeTravel;

/// <summary>
/// Controls navigation relative to one deterministic trace event.
/// </summary>
public enum TimeTravelTraceSeekMode
{
    /// <summary>
    /// Selects the latest retained checkpoint captured before the trace event existed.
    /// </summary>
    BeforeEvent = 0,

    /// <summary>
    /// Selects the earliest retained checkpoint captured after the trace event existed.
    /// </summary>
    AfterEvent = 1
}
