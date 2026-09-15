using Causalia.Runtime;

namespace Causalia.Faults;

/// <summary>
/// Evaluates a fault plan using deterministic random streams that do not perturb scheduler choices.
/// </summary>
public sealed class FaultInjector<TContext, TEffect>
{
    private readonly FaultExecutionController _executionController;
    private readonly IReadOnlyList<IFaultPolicy<TContext, TEffect>> _policies;
    private readonly string _scope;
    private readonly long _injectorId;
    private readonly ulong _streamSeed;
    private long _evaluationIndex;

    internal FaultInjector(
        FaultPlan<TContext, TEffect> plan,
        ulong streamSeed,
        string scope,
        long injectorId,
        FaultExecutionController executionController)
    {
        _policies = plan.Policies.ToList().AsReadOnly();
        _streamSeed = streamSeed;
        _scope = scope;
        _injectorId = injectorId;
        _executionController = executionController;
    }

    /// <summary>
    /// Evaluates every policy and returns the triggered effects in plan order.
    /// </summary>
    public IReadOnlyList<TEffect> Evaluate(TContext context)
    {
        var occurrence = checked(++_evaluationIndex);
        var effects = new List<TEffect>();

        foreach (var policy in _policies)
        {
            var policySeed = StableHash(policy.Name);
            var seed = DeriveSeed(_streamSeed, (ulong)occurrence, policySeed);
            var evaluationContext = new FaultEvaluationContext(seed);
            var naturallyTriggered = policy.TryEvaluate(context, evaluationContext, out var effect);
            var faultOccurrence = new FaultOccurrence(_scope, _injectorId, occurrence, policy.Name);

            if (naturallyTriggered && _executionController.ShouldApply(faultOccurrence, true))
            {
                effects.Add(effect);
            }
        }

        return effects.AsReadOnly();
    }

    internal static ulong StableHash(string value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;

        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static ulong DeriveSeed(ulong rootSeed, ulong occurrence, ulong policySeed)
    {
        var value = rootSeed ^ 0x9E3779B97F4A7C15UL;
        value = Mix(value ^ occurrence);
        return Mix(value ^ policySeed);
    }

    internal static ulong MixForStream(ulong value)
    {
        return Mix(value ^ 0xD1B54A32D192ED03UL);
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
