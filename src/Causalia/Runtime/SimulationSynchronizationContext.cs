namespace Causalia.Runtime;

internal sealed class SimulationSynchronizationContext : SynchronizationContext
{
    private readonly DeterministicScheduler _scheduler;

    public SimulationSynchronizationContext(DeterministicScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public override SynchronizationContext CreateCopy()
    {
        return this;
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _scheduler.Enqueue(callback, state);
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        callback(state);
    }
}
