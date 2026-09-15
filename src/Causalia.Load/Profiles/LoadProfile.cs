namespace Causalia.Load.Profiles;

/// <summary>
/// Defines a deterministic workload model for Causalia load execution.
/// </summary>
public abstract class LoadProfile
{
    internal abstract string Name { get; }

    internal abstract void Validate();
}
