namespace Causalia.ExampleApp;

/// <summary>
/// Stores an order marker atomically and idempotently by order identifier.
/// </summary>
public interface IOrderStore
{
    /// <summary>
    /// Returns whether the order marker already exists.
    /// </summary>
    Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the marker if absent; throws IOException for transient or ambiguous failures.
    /// </summary>
    Task CreateIfMissingAsync(string orderId, CancellationToken cancellationToken);
}
