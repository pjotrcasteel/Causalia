namespace Causalia.TimeTravel;

/// <summary>
/// Controls how a debugger cursor resolves scheduler-step navigation.
/// </summary>
public enum TimeTravelSeekMode
{
    /// <summary>
    /// Selects the latest retained checkpoint at or before the requested step.
    /// </summary>
    AtOrBefore = 0,

    /// <summary>
    /// Selects the earliest retained checkpoint at or after the requested step.
    /// </summary>
    AtOrAfter = 1
}
