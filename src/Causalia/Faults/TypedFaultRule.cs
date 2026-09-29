namespace Causalia.Faults;

/// <summary>
/// Matches a typed boundary event and produces an effect using the existing fault injector.
/// </summary>
public sealed class TypedFaultRule<TContext, TEvent, TEffect> : IFaultPolicy<TContext, TEffect>, IInjectorScopedFaultPolicy<TContext, TEffect>
    where TEvent : TContext
{
    private readonly Func<TEvent, bool> _matches;
    private readonly Func<TEvent, TEffect> _effect;
    private readonly int? _maximumOccurrences;
    private int _occurrences;

    internal TypedFaultRule(string name, Func<TEvent, bool> matches, Func<TEvent, TEffect> effect, int? maximumOccurrences)
    {
        Name = name;
        _matches = matches;
        _effect = effect;
        _maximumOccurrences = maximumOccurrences;
    }

    /// <inheritdoc />
    public string Name { get; }

    IFaultPolicy<TContext, TEffect> IInjectorScopedFaultPolicy<TContext, TEffect>.CreateForInjector() =>
        new TypedFaultRule<TContext, TEvent, TEffect>(Name, _matches, _effect, _maximumOccurrences);

    /// <inheritdoc />
    public bool TryEvaluate(TContext context, FaultEvaluationContext evaluationContext, out TEffect effect)
    {
        if (context is TEvent typed && _matches(typed) &&
            (_maximumOccurrences is null || _occurrences < _maximumOccurrences))
        {
            _occurrences++;
            effect = _effect(typed);
            return true;
        }

        effect = default!;
        return false;
    }
}
