using Causalia.Randomness;

namespace Causalia.Faults;

/// <summary>
/// Provides a deterministic random stream isolated from the scheduler for one fault-policy evaluation.
/// </summary>
public sealed class FaultEvaluationContext
{
    private readonly DeterministicRandom _random;

    internal FaultEvaluationContext(ulong seed)
    {
        Seed = seed;
        _random = new DeterministicRandom(seed);
    }

    /// <summary>
    /// Gets the derived seed for this policy evaluation.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Returns the next deterministic unsigned 64-bit value.
    /// </summary>
    public ulong NextUInt64()
    {
        return _random.NextUInt64();
    }

    /// <summary>
    /// Returns a deterministic value in the range [0, 1).
    /// </summary>
    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// Returns a deterministic integer from zero up to, but excluding, <paramref name="exclusiveMaximum"/>.
    /// </summary>
    public int NextInt32(int exclusiveMaximum)
    {
        return _random.NextInt32(exclusiveMaximum);
    }
}
