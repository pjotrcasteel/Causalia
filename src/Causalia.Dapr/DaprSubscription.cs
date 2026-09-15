namespace Causalia.Dapr;

/// <summary>
/// Represents a removable deterministic Dapr pub/sub subscription.
/// </summary>
public sealed class DaprSubscription : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    internal DaprSubscription(Action dispose)
    {
        _dispose = dispose;
    }

    /// <summary>
    /// Removes the subscription from the simulated pub/sub component.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispose();
    }
}
