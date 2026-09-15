using Causalia.Exceptions;
using Causalia.Runtime;

namespace Causalia.Invariants;

/// <summary>
/// Registers and evaluates deterministic safety and liveness invariants for one simulation run.
/// </summary>
public sealed class SimulationInvariants
{
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly List<InvariantRegistration> _registrations = new();
    private readonly DeterministicScheduler _scheduler;

    internal SimulationInvariants(DeterministicScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>
    /// Requires a condition to remain true at every deterministic observation point after registration.
    /// </summary>
    public void Always(string name, Func<bool> predicate)
    {
        RegisterSafety(name, InvariantKind.Always, predicate);
    }

    /// <summary>
    /// Requires a condition to remain false at every deterministic observation point after registration.
    /// </summary>
    public void Never(string name, Func<bool> predicate)
    {
        RegisterSafety(name, InvariantKind.Never, predicate);
    }

    /// <summary>
    /// Requires a condition to become true no later than the supplied amount of virtual time after registration.
    /// </summary>
    public void Eventually(string name, TimeSpan within, Func<bool> predicate)
    {
        ValidateRegistration(name, predicate);

        if (within < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(within), within, "Eventually requires a non-negative finite duration.");
        }

        var registeredAt = _scheduler.TimeProvider.GetUtcNow();
        var deadline = registeredAt.Add(within);
        var registration = new InvariantRegistration(name, InvariantKind.Eventually, predicate, registeredAt, deadline);
        AddRegistration(registration);
        _scheduler.RecordTrace($"invariant:registered:eventually:{name}:deadline:{deadline.Ticks}");

        if (TryEvaluate(registration))
        {
            CompleteEventually(registration, registeredAt);
            return;
        }

        if (within == TimeSpan.Zero)
        {
            ThrowViolation(registration, registeredAt);
        }

        var completion = new TaskCompletionSource();
        registration.Completion = completion;
        _scheduler.TrackOperation(completion.Task);
        registration.DeadlineTimer = _scheduler.TimeProvider.CreateTimer(
            static state =>
            {
                var deadlineState = (InvariantDeadlineState)state!;
                deadlineState.Invariants.RecordDeadlineWakeup(deadlineState.Registration);
            },
            new InvariantDeadlineState(this, registration),
            within,
            Timeout.InfiniteTimeSpan);
    }

    internal void Observe()
    {
        var now = _scheduler.TimeProvider.GetUtcNow();

        foreach (var registration in _registrations)
        {
            if (registration.IsCompleted)
            {
                continue;
            }

            var value = TryEvaluate(registration);

            switch (registration.Kind)
            {
                case InvariantKind.Always when !value:
                    ThrowViolation(registration, now);
                    break;
                case InvariantKind.Never when value:
                    ThrowViolation(registration, now);
                    break;
                case InvariantKind.Eventually when value:
                    CompleteEventually(registration, now);
                    break;
            }
        }
    }

    internal void ThrowIfExpiredAtQuiescence()
    {
        var now = _scheduler.TimeProvider.GetUtcNow();

        foreach (var registration in _registrations)
        {
            if (registration.IsCompleted || registration.Kind != InvariantKind.Eventually || registration.Deadline > now)
            {
                continue;
            }

            if (TryEvaluate(registration))
            {
                CompleteEventually(registration, now);
                continue;
            }

            ThrowViolation(registration, now);
        }
    }

    internal IReadOnlyList<InvariantOutcome> Complete()
    {
        Observe();
        ThrowIfExpiredAtQuiescence();
        var completedAt = _scheduler.TimeProvider.GetUtcNow();
        var outcomes = new List<InvariantOutcome>(_registrations.Count);

        foreach (var registration in _registrations)
        {
            if (registration.Kind != InvariantKind.Eventually)
            {
                registration.CompletedAt = completedAt;
            }

            if (!registration.CompletedAt.HasValue)
            {
                throw new InvalidOperationException($"Invariant '{registration.Name}' remained incomplete when the simulation ended.");
            }

            outcomes.Add(
                new InvariantOutcome(
                    registration.Name,
                    registration.Kind,
                    registration.RegisteredAt,
                    registration.CompletedAt.Value));
        }

        return outcomes.AsReadOnly();
    }

    private void RegisterSafety(string name, InvariantKind kind, Func<bool> predicate)
    {
        ValidateRegistration(name, predicate);
        var now = _scheduler.TimeProvider.GetUtcNow();
        var registration = new InvariantRegistration(name, kind, predicate, now, null);
        AddRegistration(registration);
        _scheduler.RecordTrace($"invariant:registered:{kind.ToString().ToLowerInvariant()}:{name}");
        var value = TryEvaluate(registration);

        if ((kind == InvariantKind.Always && !value) || (kind == InvariantKind.Never && value))
        {
            ThrowViolation(registration, now);
        }
    }

    private void AddRegistration(InvariantRegistration registration)
    {
        if (!_names.Add(registration.Name))
        {
            throw new InvalidOperationException($"An invariant named '{registration.Name}' is already registered in this simulation run.");
        }

        _registrations.Add(registration);
    }

    private bool TryEvaluate(InvariantRegistration registration)
    {
        try
        {
            return registration.Predicate();
        }
        catch (SimulationInvariantException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var now = _scheduler.TimeProvider.GetUtcNow();
            _scheduler.RecordTrace($"invariant:evaluation-failed:{registration.Kind.ToString().ToLowerInvariant()}:{registration.Name}");
            throw new SimulationInvariantEvaluationException(registration.Name, registration.Kind, now, exception);
        }
    }

    private void CompleteEventually(InvariantRegistration registration, DateTimeOffset completedAt)
    {
        if (registration.IsCompleted)
        {
            return;
        }

        registration.CompletedAt = completedAt;
        registration.DeadlineTimer?.Dispose();
        registration.Completion?.TrySetResult();
        _scheduler.RecordTrace($"invariant:satisfied:eventually:{registration.Name}");
    }

    private void ThrowViolation(InvariantRegistration registration, DateTimeOffset observedAt)
    {
        _scheduler.RecordTrace($"invariant:violated:{registration.Kind.ToString().ToLowerInvariant()}:{registration.Name}");
        throw new SimulationInvariantViolationException(registration.Name, registration.Kind, observedAt, registration.Deadline);
    }

    private void RecordDeadlineWakeup(InvariantRegistration registration)
    {
        if (!registration.IsCompleted)
        {
            _scheduler.RecordTrace($"invariant:deadline:eventually:{registration.Name}");
        }
    }

    private static void ValidateRegistration(string name, Func<bool> predicate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(predicate);
    }
}
