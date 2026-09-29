namespace Causalia.Faults;

/// <summary>
/// Describes an ordered, composable set of deterministic fault policies.
/// </summary>
public sealed class FaultPlan<TContext, TEffect>
{
    private readonly List<IFaultPolicy<TContext, TEffect>> _policies = new();

    internal IReadOnlyList<IFaultPolicy<TContext, TEffect>> Policies => _policies;

    /// <summary>Begins a typed rule for an adapter event within this fault plan.</summary>
    public TypedFaultRuleBuilder<TContext, TEvent, TEffect> On<TEvent>(string name)
        where TEvent : TContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TypedFaultRuleBuilder<TContext, TEvent, TEffect>(this, name);
    }

    /// <summary>
    /// Adds a uniquely named policy to the plan.
    /// </summary>
    public FaultPlan<TContext, TEffect> Add(IFaultPolicy<TContext, TEffect> policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (_policies.Any(existing => string.Equals(existing.Name, policy.Name, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"A fault policy named '{policy.Name}' is already registered.", nameof(policy));
        }

        _policies.Add(policy);
        return this;
    }
}
