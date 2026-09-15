using Causalia.Testing;

namespace Causalia.Reqnroll;

/// <summary>
/// Scenario-scoped provider that can be injected directly into Reqnroll binding constructors.
/// </summary>
/// <remarks>
/// Reqnroll creates ordinary context classes per scenario, so this adapter intentionally does not require
/// Reqnroll types or a custom runtime plugin. This keeps Reqnroll versioning outside the Causalia core graph.
/// </remarks>
public sealed class ReqnrollSimulationProvider : SimulationProvider
{
}
