using Causalia.Minimization;
using Causalia.Scheduling;

namespace Causalia.Runtime;

internal sealed class ScheduleExecutionOptions
{
    private ScheduleExecutionOptions(
        ScheduleExecutionMode mode,
        IReadOnlyList<int> systematicPrefix,
        bool preferNonPreemptiveChoices,
        SimulationSchedule? replaySchedule,
        SimulationReproduction? reproduction)
    {
        Mode = mode;
        SystematicPrefix = systematicPrefix;
        PreferNonPreemptiveChoices = preferNonPreemptiveChoices;
        ReplaySchedule = replaySchedule;
        Reproduction = reproduction;
    }

    public ScheduleExecutionMode Mode { get; }

    public IReadOnlyList<int> SystematicPrefix { get; }

    public bool PreferNonPreemptiveChoices { get; }

    public SimulationSchedule? ReplaySchedule { get; }

    public SimulationReproduction? Reproduction { get; }

    public static ScheduleExecutionOptions Random()
    {
        return new ScheduleExecutionOptions(ScheduleExecutionMode.Random, Array.Empty<int>(), false, null, null);
    }

    public static ScheduleExecutionOptions Systematic(IReadOnlyList<int> prefix, bool preferNonPreemptiveChoices = false)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return new ScheduleExecutionOptions(ScheduleExecutionMode.Systematic, prefix, preferNonPreemptiveChoices, null, null);
    }

    public static ScheduleExecutionOptions Replay(SimulationSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return new ScheduleExecutionOptions(ScheduleExecutionMode.Replay, Array.Empty<int>(), false, schedule, null);
    }

    public static ScheduleExecutionOptions ForReproduction(SimulationReproduction reproduction)
    {
        ArgumentNullException.ThrowIfNull(reproduction);
        return new ScheduleExecutionOptions(ScheduleExecutionMode.Reproduction, Array.Empty<int>(), false, null, reproduction);
    }
}
