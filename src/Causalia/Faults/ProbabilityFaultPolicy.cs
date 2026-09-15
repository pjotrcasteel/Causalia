namespace Causalia.Faults;

/// <summary>
/// Produces an effect when a deterministic probability check succeeds.
/// </summary>
public sealed class ProbabilityFaultPolicy<TContext, TEffect> : IFaultPolicy<TContext, TEffect>
{
    private readonly Func<TContext, TEffect> _effectFactory;
    private readonly double _probability;

    /// <summary>
    /// Initializes a probability-based fault policy.
    /// </summary>
    public ProbabilityFaultPolicy(string name, double probability, Func<TContext, TEffect> effectFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(effectFactory);

        if (double.IsNaN(probability) || probability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability), probability, "Probability must be between zero and one inclusive.");
        }

        Name = name;
        _probability = probability;
        _effectFactory = effectFactory;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public bool TryEvaluate(TContext context, FaultEvaluationContext evaluationContext, out TEffect effect)
    {
        ArgumentNullException.ThrowIfNull(evaluationContext);

        if (evaluationContext.NextDouble() >= _probability)
        {
            effect = default!;
            return false;
        }

        effect = _effectFactory(context);
        return true;
    }
}
