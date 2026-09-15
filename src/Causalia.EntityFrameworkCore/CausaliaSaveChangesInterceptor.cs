using System.Runtime.CompilerServices;
using Causalia.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Causalia.EntityFrameworkCore;

/// <summary>
/// Maps EF Core SaveChangesAsync onto Causalia's deterministic before-commit and after-commit storage boundaries.
/// </summary>
public sealed class CausaliaSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly SimulationStorageBoundary _boundary;
    private readonly ConditionalWeakTable<DbContext, StorageOperationLeaseHolder> _leases = new();

    /// <summary>
    /// Initializes the interceptor with the deterministic storage boundary used for commit simulation.
    /// </summary>
    public CausaliaSaveChangesInterceptor(SimulationStorageBoundary boundary)
    {
        _boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new SynchronousEfCoreOperationNotSupportedException();
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = RequireContext(eventData);
        _leases.Remove(context);
        var lease = await _boundary.BeginAsync(StorageOperationKind.Commit, null, cancellationToken);
        _leases.Add(context, new StorageOperationLeaseHolder(lease));
        return result;
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        throw new SynchronousEfCoreOperationNotSupportedException();
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = RequireContext(eventData);

        if (!_leases.TryGetValue(context, out var holder))
        {
            throw new InvalidOperationException("Causalia did not observe the matching EF Core before-commit boundary.");
        }

        _leases.Remove(context);
        await _boundary.CompleteAsync(holder.Lease, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        HandleProviderFailure(eventData);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        HandleProviderFailure(eventData);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        HandleProviderCancellation(eventData, CancellationToken.None);
    }

    /// <inheritdoc />
    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        HandleProviderCancellation(eventData, cancellationToken);
        return Task.CompletedTask;
    }

    private void HandleProviderFailure(DbContextErrorEventData eventData)
    {
        var context = RequireContext(eventData);

        if (!_leases.TryGetValue(context, out var holder))
        {
            return;
        }

        _leases.Remove(context);
        _boundary.ProviderFailed(holder.Lease, eventData.Exception);
    }

    private void HandleProviderCancellation(DbContextEventData eventData, CancellationToken cancellationToken)
    {
        var context = RequireContext(eventData);

        if (!_leases.TryGetValue(context, out var holder))
        {
            return;
        }

        _leases.Remove(context);
        _boundary.ProviderCanceled(holder.Lease, cancellationToken);
    }

    private static DbContext RequireContext(DbContextEventData eventData)
    {
        return eventData.Context ?? throw new InvalidOperationException("EF Core did not provide the DbContext for the SaveChanges interception.");
    }

}
