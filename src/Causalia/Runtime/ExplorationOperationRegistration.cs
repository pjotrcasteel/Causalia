using Causalia.Exploration;

namespace Causalia.Runtime;

internal sealed class ExplorationOperationRegistration
{
    public required long Id { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<ExplorationResourceAccess> Accesses { get; init; }
}
