using Causalia.Exceptions;
using Causalia.Minimization;

namespace Causalia.Runtime;

internal static class SimulationMinimizer
{
    public static MinimizationResult Minimize(
        SimulationOptions simulationOptions,
        MinimizationOptions minimizationOptions,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        Validate(simulationOptions, minimizationOptions, failure, scenario);
        var fingerprint = FailureFingerprint.Create(failure);
        var schedulerElements = failure.Schedule.Decisions
            .Where(decision => decision.SelectedIndex != 0)
            .Select(decision => MinimizationElement.ForScheduler(new SchedulerChoice(decision.DecisionIndex, decision.SelectedIndex)))
            .ToList();
        var faultElements = failure.Faults
            .Select(MinimizationElement.ForFault)
            .ToList();
        var fixedElements = new List<MinimizationElement>();
        var candidates = new List<MinimizationElement>();

        AddElements(schedulerElements, minimizationOptions.MinimizeSchedulerChoices, fixedElements, candidates);
        AddElements(faultElements, minimizationOptions.MinimizeFaults, fixedElements, candidates);

        var state = new MinimizationState
        {
            SimulationOptions = simulationOptions,
            MinimizationOptions = minimizationOptions,
            FixedElements = fixedElements.AsReadOnly(),
            Candidates = candidates,
            Fingerprint = fingerprint,
            Scenario = scenario,
            CancellationToken = cancellationToken
        };
        var initial = CreateReproduction(simulationOptions.Seed, fixedElements.Concat(candidates));
        var initialFailure = TryReproduce(state, initial);

        if (initialFailure is null)
        {
            throw new SimulationMinimizationException(
                "The supplied failure could not be reproduced using its non-canonical scheduler choices and triggered faults.");
        }

        MinimizeCandidates(state);

        var reproduction = CreateReproduction(simulationOptions.Seed, fixedElements.Concat(candidates));
        var minimizedFailure = TryReproduceWithoutBudget(
            simulationOptions,
            reproduction,
            fingerprint,
            scenario,
            cancellationToken)
            ?? throw new SimulationMinimizationException("The final minimized reproduction no longer reproduces the original failure.");

        return new MinimizationResult(failure, minimizedFailure, reproduction, state.Attempts, state.ExhaustedBudget);
    }

    private static void MinimizeCandidates(MinimizationState state)
    {
        if (state.Candidates.Count == 0)
        {
            return;
        }

        var granularity = Math.Min(2, state.Candidates.Count);

        while (state.Candidates.Count > 0)
        {
            if (state.Attempts >= state.MinimizationOptions.MaxAttempts)
            {
                state.ExhaustedBudget = true;
                return;
            }

            if (state.Candidates.Count == 1)
            {
                TryRemoveOnlyCandidate(state);
                return;
            }

            granularity = Math.Min(granularity, state.Candidates.Count);
            var reduced = false;

            foreach (var range in CreatePartitions(state.Candidates.Count, granularity))
            {
                if (state.Attempts >= state.MinimizationOptions.MaxAttempts)
                {
                    state.ExhaustedBudget = true;
                    return;
                }

                var complement = state.Candidates
                    .Where((_, index) => index < range.Start || index >= range.End)
                    .ToList();
                var reproduction = CreateReproduction(state.SimulationOptions.Seed, state.FixedElements.Concat(complement));
                var reproduced = TryReproduce(state, reproduction);

                if (reproduced is null)
                {
                    continue;
                }

                state.Candidates.Clear();
                state.Candidates.AddRange(complement);
                granularity = Math.Max(2, granularity - 1);
                reduced = true;
                break;
            }

            if (reduced)
            {
                continue;
            }

            if (granularity >= state.Candidates.Count)
            {
                return;
            }

            granularity = Math.Min(state.Candidates.Count, granularity * 2);
        }
    }

    private static void TryRemoveOnlyCandidate(MinimizationState state)
    {
        if (state.Attempts >= state.MinimizationOptions.MaxAttempts)
        {
            state.ExhaustedBudget = true;
            return;
        }

        var reproduction = CreateReproduction(state.SimulationOptions.Seed, state.FixedElements);
        var reproduced = TryReproduce(state, reproduction);

        if (reproduced is not null)
        {
            state.Candidates.Clear();
        }
    }

    private static IReadOnlyList<PartitionRange> CreatePartitions(int count, int granularity)
    {
        var partitions = new List<PartitionRange>(granularity);
        var baseSize = count / granularity;
        var remainder = count % granularity;
        var start = 0;

        for (var index = 0; index < granularity; index++)
        {
            var size = baseSize + (index < remainder ? 1 : 0);
            partitions.Add(new PartitionRange(start, start + size));
            start += size;
        }

        return partitions.AsReadOnly();
    }

    private static SimulationFailedException? TryReproduce(MinimizationState state, SimulationReproduction reproduction)
    {
        state.Attempts++;
        return TryReproduceWithoutBudget(
            state.SimulationOptions,
            reproduction,
            state.Fingerprint,
            state.Scenario,
            state.CancellationToken);
    }

    private static SimulationFailedException? TryReproduceWithoutBudget(
        SimulationOptions options,
        SimulationReproduction reproduction,
        FailureFingerprint fingerprint,
        Func<SimulationContext, Task> scenario,
        CancellationToken cancellationToken)
    {
        try
        {
            var scheduler = new DeterministicScheduler(options, ScheduleExecutionOptions.ForReproduction(reproduction), cancellationToken);
            scheduler.Run(scenario);
            return null;
        }
        catch (SimulationFailedException exception)
        {
            return FailureFingerprint.Create(exception) == fingerprint ? exception : null;
        }
        catch (SimulationReproductionDivergedException)
        {
            return null;
        }
    }

    private static SimulationReproduction CreateReproduction(ulong seed, IEnumerable<MinimizationElement> elements)
    {
        var materialized = elements.ToList();
        var schedulerChoices = materialized
            .Where(element => element.SchedulerChoice is not null)
            .Select(element => element.SchedulerChoice!)
            .OrderBy(choice => choice.DecisionIndex)
            .ToList()
            .AsReadOnly();
        var faults = materialized
            .Where(element => element.Fault is not null)
            .Select(element => element.Fault!)
            .OrderBy(fault => fault.Scope, StringComparer.Ordinal)
            .ThenBy(fault => fault.InjectorId)
            .ThenBy(fault => fault.Occurrence)
            .ThenBy(fault => fault.PolicyName, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
        return new SimulationReproduction(seed, schedulerChoices, faults);
    }

    private static void AddElements(
        IEnumerable<MinimizationElement> source,
        bool canMinimize,
        ICollection<MinimizationElement> fixedElements,
        ICollection<MinimizationElement> candidates)
    {
        var target = canMinimize ? candidates : fixedElements;

        foreach (var element in source)
        {
            target.Add(element);
        }
    }

    private static void Validate(
        SimulationOptions simulationOptions,
        MinimizationOptions minimizationOptions,
        SimulationFailedException failure,
        Func<SimulationContext, Task> scenario)
    {
        ArgumentNullException.ThrowIfNull(simulationOptions);
        ArgumentNullException.ThrowIfNull(minimizationOptions);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(scenario);

        if (minimizationOptions.MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimizationOptions),
                minimizationOptions.MaxAttempts,
                "MaxAttempts must be greater than zero.");
        }

        if (simulationOptions.Seed != failure.Seed)
        {
            throw new ArgumentException(
                $"Simulation seed {simulationOptions.Seed} does not match failure seed {failure.Seed}.",
                nameof(simulationOptions));
        }
    }
}
