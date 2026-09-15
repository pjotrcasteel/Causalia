namespace Causalia.Minimization;

internal sealed record ReproductionPayload(
    ulong Seed,
    SchedulerChoicePayload[] SchedulerChoices,
    FaultPayload[] Faults);
