namespace Causalia.Time;

internal sealed class ScheduledTimerInvocation
{
    public ScheduledTimerInvocation(SimulationTimer timer, long generation, long? operationId)
    {
        Timer = timer;
        Generation = generation;
        OperationId = operationId;
    }

    public SimulationTimer Timer { get; }

    public long Generation { get; }

    public long? OperationId { get; }
}
