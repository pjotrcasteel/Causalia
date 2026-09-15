using Causalia.FailureIntelligence;
using Causalia.Exceptions;
using Causalia.Scheduling;

namespace Causalia.ModelBased;

/// <summary>
/// Reports the first deterministic failure discovered while exploring executable model command sequences.
/// </summary>
public sealed class SimulationModelExplorationFailedException : Exception
{
    internal SimulationModelExplorationFailedException(
        string modelName,
        int sequencesExplored,
        int schedulesExplored,
        ModelSequence sequence,
        SimulationFailedException failure)
        : base(
            $"Model '{modelName}' found a failure after {sequencesExplored} sequence(s) and {schedulesExplored} schedule(s). " +
            $"Model replay token: {sequence.ReplayToken}. Scheduler replay token: {failure.Schedule.ReplayToken}",
            failure)
    {
        ModelName = modelName;
        SequencesExplored = sequencesExplored;
        SchedulesExplored = schedulesExplored;
        Sequence = sequence;
        Failure = failure;
        Schedule = failure.Schedule;
        ModelFailure = failure.InnerException as SimulationModelViolationException;
    }

    /// <summary>
    /// Gets the executable model name.
    /// </summary>
    public string ModelName { get; }

    /// <summary>
    /// Gets the number of model sequences executed before and including the failing sequence.
    /// </summary>
    public int SequencesExplored { get; }

    /// <summary>
    /// Gets the total number of scheduler timelines executed before and including the failing timeline.
    /// </summary>
    public int SchedulesExplored { get; }

    /// <summary>
    /// Gets the model command sequence that exposed the failure.
    /// </summary>
    public ModelSequence Sequence { get; }

    /// <summary>
    /// Gets the deterministic simulation failure discovered by model exploration.
    /// </summary>
    public SimulationFailedException Failure { get; }

    /// <summary>
    /// Gets the exact scheduler schedule that exposed the failure.
    /// </summary>
    public SimulationSchedule Schedule { get; }

    /// <summary>
    /// Gets the structured model mismatch when the failure was produced by a model verifier.
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
