namespace Causalia.AspNetCore.Internal;

internal static class SimulationHttpTaskBridge
{
    public static Task<T> Bridge<T>(Task<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.IsCompleted)
        {
            return operation;
        }

        var completion = new TaskCompletionSource<T>();
        _ = CompleteAsync(operation, completion);
        return completion.Task;
    }

    private static async Task CompleteAsync<T>(Task<T> operation, TaskCompletionSource<T> completion)
    {
        try
        {
            var result = await operation;
            CompleteWithoutSynchronizationContext(() => completion.SetResult(result));
        }
        catch (OperationCanceledException exception)
        {
            CompleteWithoutSynchronizationContext(() => completion.SetCanceled(exception.CancellationToken));
        }
        catch (Exception exception)
        {
            CompleteWithoutSynchronizationContext(() => completion.SetException(exception));
        }
    }

    private static void CompleteWithoutSynchronizationContext(Action complete)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);

        try
        {
            complete();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
