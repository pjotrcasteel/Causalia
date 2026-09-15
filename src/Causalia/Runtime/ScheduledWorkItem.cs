namespace Causalia.Runtime;

internal sealed class ScheduledWorkItem
{
    public ScheduledWorkItem(
        long id,
        SendOrPostCallback callback,
        object? state,
        long? parentWorkItemId,
        long? operationId)
    {
        Id = id;
        Callback = callback;
        State = state;
        ParentWorkItemId = parentWorkItemId;
        OperationId = operationId;
    }

    public long Id { get; }

    public SendOrPostCallback Callback { get; }

    public object? State { get; }

    public long? ParentWorkItemId { get; }

    public long? OperationId { get; }
}
