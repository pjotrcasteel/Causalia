using Causalia.Storage;
using Microsoft.EntityFrameworkCore;

namespace Causalia.EntityFrameworkCore;

/// <summary>
/// Registers deterministic Causalia SaveChanges boundaries on EF Core DbContext options.
/// </summary>
public static class CausaliaDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds deterministic Causalia SaveChanges simulation to a non-generic EF Core options builder.
    /// </summary>
    public static DbContextOptionsBuilder AddCausaliaStorageSimulation(
        this DbContextOptionsBuilder optionsBuilder,
        SimulationStorageBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(boundary);
        return optionsBuilder.AddInterceptors(new CausaliaSaveChangesInterceptor(boundary));
    }

    /// <summary>
    /// Adds deterministic Causalia SaveChanges simulation to a typed EF Core options builder.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> AddCausaliaStorageSimulation<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        SimulationStorageBoundary boundary)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(boundary);
        optionsBuilder.AddInterceptors(new CausaliaSaveChangesInterceptor(boundary));
        return optionsBuilder;
    }
}
