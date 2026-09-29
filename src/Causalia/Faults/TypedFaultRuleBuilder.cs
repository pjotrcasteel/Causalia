namespace Causalia.Faults;

/// <summary>
/// Configures a typed event match for an existing fault plan.
/// </summary>
public sealed class TypedFaultRuleBuilder<TContext, TEvent, TEffect>
    where TEvent : TContext
{
    private readonly FaultPlan<TContext, TEffect> _plan;
    private readonly string _name;
    private Func<TEvent, bool> _matches = static _ => true;
    private int? _maximumOccurrences;

    internal TypedFaultRuleBuilder(FaultPlan<TContext, TEffect> plan, string name)
    {
        _plan = plan;
        _name = name;
    }

    /// <summary>Restricts the rule to events satisfying the predicate.</summary>
    public TypedFaultRuleBuilder<TContext, TEvent, TEffect> Where(Func<TEvent, bool> matches)
    {
        _matches = matches ?? throw new ArgumentNullException(nameof(matches));
        return this;
    }

    /// <summary>Triggers at most once per injector instance.</summary>
    public TypedFaultRuleBuilder<TContext, TEvent, TEffect> Once()
    {
        _maximumOccurrences = 1;
        return this;
    }

    /// <summary>Triggers for at most the specified number of matching events.</summary>
    public TypedFaultRuleBuilder<TContext, TEvent, TEffect> Times(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        _maximumOccurrences = count;
        return this;
    }

    /// <summary>Adds the rule to the existing plan in registration order.</summary>
    public FaultPlan<TContext, TEffect> Apply(Func<TEvent, TEffect> effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return _plan.Add(new TypedFaultRule<TContext, TEvent, TEffect>(_name, _matches, effect, _maximumOccurrences));
    }
}
