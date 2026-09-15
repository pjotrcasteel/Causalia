namespace Causalia.ExampleApp;

/// <summary>
/// Demonstrates retrying an idempotent operation using an injected clock and storage port.
/// </summary>
public sealed class OrderService
{
    private readonly IOrderStore _store;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes the service with its storage boundary and clock.
    /// </summary>
    public OrderService(IOrderStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Accepts an order once, retrying recoverable storage failures at most twice.
    /// </summary>
    public async Task AcceptAsync(string orderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!await _store.ExistsAsync(orderId, cancellationToken))
                {
                    await _store.CreateIfMissingAsync(orderId, cancellationToken);
                }

                return;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), _timeProvider, cancellationToken);
            }
        }
    }
}
