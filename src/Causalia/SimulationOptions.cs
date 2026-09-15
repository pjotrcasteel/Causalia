using Causalia.TimeTravel;

namespace Causalia;

/// <summary>
/// Configures one deterministic simulation run.
/// </summary>
public sealed class SimulationOptions
{
    /// <summary>
    /// Gets or initializes the deterministic seed used to select runnable work.
    /// </summary>
    public ulong Seed { get; init; } = 1;

    /// <summary>
    /// Gets or initializes the maximum number of scheduler steps before the run is aborted.
    /// </summary>
    public int MaxSteps { get; init; } = 100_000;

    /// <summary>
    /// Gets or initializes the UTC instant at which virtual time starts.
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets or initializes whether scheduler work-item and decision events are written to the simulation trace.
    /// Schedule capture and replay remain enabled when this is false.
    /// </summary>
    public bool TraceSchedulerEvents { get; init; } = true;

    /// <summary>
    /// Gets or initializes deterministic checkpoint capture for the time-travel debugger.
    /// </summary>
    public TimeTravelOptions TimeTravel { get; init; } = new();
}
