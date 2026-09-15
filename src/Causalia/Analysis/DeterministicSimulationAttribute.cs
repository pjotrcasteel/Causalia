namespace Causalia;

/// <summary>
/// Marks code that must remain inside Causalia's deterministic execution model.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
public sealed class DeterministicSimulationAttribute : Attribute
{
}
