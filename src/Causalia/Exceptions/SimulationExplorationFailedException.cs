using Causalia.FailureIntelligence;
using Causalia.Faults;
using Causalia.Scheduling;
using Causalia.ModelBased;
using Causalia.Tracing;

namespace Causalia.Exceptions;

/// <summary>
/// Reports the first scenario failure discovered during bounded schedule exploration.
/// </summary>
public sealed class SimulationExplorationFailedException : Exception
{
    internal SimulationExplorationFailedException(
        ExplorationStrategy strategy,
        int schedulesExplored,
        SimulationFailedException failure)
        : base(
            $"{strategy} exploration found a failure after {schedulesExplored} schedule(s). " +
            $"Replay token: {failure.Schedule.ReplayToken}",
            failure)
    {
        Strategy = strategy;
        SchedulesExplored = schedulesExplored;
        Failure = failure;
        Seed = failure.Seed;
        Schedule = failure.Schedule;
        Trace = failure.Trace;
        Faults = failure.Faults;
        InvariantFailure = failure.InvariantFailure;
        ModelFailure = failure.ModelFailure;
    }

    /// <summary>
    /// Gets the exploration strategy that discovered the failure.
    /// </summary>
    public ExplorationStrategy Strategy { get; }

    /// <summary>
    /// Gets the number of schedules executed before and including the failing schedule.
    /// </summary>
    public int SchedulesExplored { get; }

    /// <summary>
    /// Gets the deterministic failure discovered by exploration.
    /// </summary>
    public SimulationFailedException Failure { get; }

    /// <summary>
    /// Gets the simulation seed used while exploring schedules.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the exact failing schedule for deterministic replay.
    /// </summary>
    public SimulationSchedule Schedule { get; }

    /// <summary>
    /// Gets the trace captured by the failing schedule.
    /// </summary>
    public IReadOnlyList<SimulationTraceEntry> Trace { get; }

    /// <summary>
    /// Gets the deterministic fault occurrences applied before the failure.
    /// </summary>
    public IReadOnlyList<FaultOccurrence> Faults { get; }

    /// <summary>
    /// Gets the invariant failure when exploration stopped because an invariant failed.
    /// </summary>
    public SimulationInvariantException? InvariantFailure { get; }

    /// <summary>
    /// Gets the model-based verification failure when exploration stopped because an executable model command failed.
    /// </summary>
    public SimulationModelViolationException? ModelFailure { get; }
    /// <summary>
    /// Gets the stable semantic signature of the discovered failure.
    /// </summary>
    public FailureSignature Signature => Failure.Signature;

    /// <summary>
    /// Gets the semantic classification of the discovered failure.
    /// </summary>
    public FailureKind Kind => Failure.Kind;

}
