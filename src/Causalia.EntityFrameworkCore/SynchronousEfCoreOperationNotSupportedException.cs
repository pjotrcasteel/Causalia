namespace Causalia.EntityFrameworkCore;

/// <summary>
/// Indicates that a synchronous EF Core save bypassed deterministic asynchronous storage simulation.
/// </summary>
public sealed class SynchronousEfCoreOperationNotSupportedException : InvalidOperationException
{
    internal SynchronousEfCoreOperationNotSupportedException()
        : base("Use SaveChangesAsync with Causalia.EntityFrameworkCore. Synchronous SaveChanges cannot advance Causalia virtual time deterministically.")
    {
    }
}
