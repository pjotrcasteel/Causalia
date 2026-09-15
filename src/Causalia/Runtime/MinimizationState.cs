using Causalia.Minimization;

namespace Causalia.Runtime;

internal sealed class MinimizationState
{
    public required SimulationOptions SimulationOptions { get; init; }

    public required MinimizationOptions MinimizationOptions { get; init; }

    public required IReadOnlyList<MinimizationElement> FixedElements { get; init; }

    public required List<MinimizationElement> Candidates { get; init; }

    public required FailureFingerprint Fingerprint { get; init; }

    public required Func<SimulationContext, Task> Scenario { get; init; }

    public required CancellationToken CancellationToken { get; init; }

    public int Attempts { get; set; }

    public bool ExhaustedBudget { get; set; }
}
