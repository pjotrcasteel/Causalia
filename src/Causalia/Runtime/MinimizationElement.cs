using Causalia.Faults;
using Causalia.Minimization;

namespace Causalia.Runtime;

internal sealed record MinimizationElement(SchedulerChoice? SchedulerChoice, FaultOccurrence? Fault)
{
    public static MinimizationElement ForScheduler(SchedulerChoice choice)
    {
        return new MinimizationElement(choice, null);
    }

    public static MinimizationElement ForFault(FaultOccurrence fault)
    {
        return new MinimizationElement(null, fault);
    }
}
