using Causalia.Exceptions;
using Causalia.FailureIntelligence;

namespace Causalia.Runtime;

internal sealed record FailureFingerprint(FailureSignature Signature)
{
    public static FailureFingerprint Create(SimulationFailedException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new FailureFingerprint(failure.Signature);
    }
}
