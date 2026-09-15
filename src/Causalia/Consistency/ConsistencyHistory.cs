using Causalia.Runtime;

namespace Causalia.Consistency;

/// <summary>
/// Records logical reads, writes and replica snapshots and verifies distributed-consistency guarantees.
/// </summary>
/// <typeparam name="TKey">The logical key type used by the distributed data set.</typeparam>
public sealed class ConsistencyHistory<TKey> : IConsistencyHistory
    where TKey : notnull
{
    private readonly List<ConsistencyConvergenceRegistration> _convergence = new();
    private readonly HashSet<string> _convergenceNames = new(StringComparer.Ordinal);
    private readonly IEqualityComparer<TKey> _keyComparer;
    private readonly Dictionary<string, Dictionary<TKey, ConsistencyVersion<TKey>?>> _replicas = new(StringComparer.Ordinal);
    private readonly DeterministicScheduler _scheduler;
    private readonly Dictionary<string, ConsistencySessionState<TKey>> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<long, ConsistencyVersion<TKey>> _versions = new();
    private long _nextVersionId;
    private int _reads;
    private int _replicaObservations;

    internal ConsistencyHistory(string name, DeterministicScheduler scheduler, IEqualityComparer<TKey> keyComparer)
    {
        Name = name;
        _scheduler = scheduler;
        _keyComparer = keyComparer;
    }

    /// <summary>
    /// Gets the logical history name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the consistency guarantees enabled for this history.
    /// </summary>
    public ConsistencyGuarantees Guarantees { get; private set; }

    /// <summary>
    /// Enables one or more consistency guarantees for this history before observations are recorded.
    /// </summary>
    public ConsistencyHistory<TKey> Require(ConsistencyGuarantees guarantees)
    {
        if (guarantees == ConsistencyGuarantees.None)
        {
            return this;
        }

        if (_versions.Count > 0 || _reads > 0 || _replicaObservations > 0)
        {
            throw new InvalidOperationException("Consistency guarantees must be configured before recording history observations.");
        }

        Guarantees |= guarantees;
        _scheduler.RecordTrace($"consistency:{Name}:require:{Guarantees}");
        return this;
    }

    /// <summary>
    /// Records one logical write and returns the version that later reads or replica snapshots may reference.
    /// </summary>
    public ConsistencyVersion<TKey> Write(string sessionId, string replicaId, TKey key)
    {
        ValidateIdentifier(sessionId, nameof(sessionId));
        ValidateIdentifier(replicaId, nameof(replicaId));
        ArgumentNullException.ThrowIfNull(key);

        var session = GetSession(sessionId);
        var causalPredecessors = session.CausalVersionIds.OrderBy(id => id).ToArray();
        var readPredecessors = session.ReadVersionIds.OrderBy(id => id).ToArray();
        var metadata = new ConsistencyVersionMetadata
        {
            CausalPredecessorIds = causalPredecessors,
            ReadPredecessorIds = readPredecessors,
            SessionWriteSequence = checked(++session.Writes)
        };
        var version = new ConsistencyVersion<TKey>(
            checked(++_nextVersionId),
            key,
            sessionId,
            replicaId,
            _scheduler.TimeProvider.GetUtcNow(),
            metadata);

        _versions.Add(version.Id, version);
        session.CausalVersionIds.Add(version.Id);
        session.LastWrites[key] = version;
        _scheduler.RecordTrace(
            $"consistency:{Name}:write:{version.Id}:session:{sessionId}:replica:{replicaId}:key:{key}");
        _scheduler.Coverage.RecordAutomatic($"consistency:{Name}:write");
        return version;
    }

    /// <summary>
    /// Records a logical read. A null version represents an observation that the key has no visible value.
    /// </summary>
    public void Read(string sessionId, string replicaId, TKey key, ConsistencyVersion<TKey>? observedVersion)
    {
        ValidateIdentifier(sessionId, nameof(sessionId));
        ValidateIdentifier(replicaId, nameof(replicaId));
        ArgumentNullException.ThrowIfNull(key);
        ValidateVersion(key, observedVersion, nameof(observedVersion));

        var session = GetSession(sessionId);
        VerifyReadYourWrites(sessionId, key, observedVersion, session);
        VerifyMonotonicReads(sessionId, key, observedVersion, session);
        VerifyReplicaReadAgreement(sessionId, replicaId, key, observedVersion);

        session.LastReads[key] = observedVersion;
        if (observedVersion is not null)
        {
            AddCausalClosure(session, observedVersion);
            session.ReadVersionIds.Add(observedVersion.Id);
        }

        _reads++;
        var versionText = observedVersion?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
        _scheduler.RecordTrace(
            $"consistency:{Name}:read:{_reads}:session:{sessionId}:replica:{replicaId}:key:{key}:version:{versionText}");
        _scheduler.Coverage.RecordAutomatic($"consistency:{Name}:read");
    }

    /// <summary>
    /// Replaces the known logical snapshot for one replica and verifies replica-scoped guarantees against it.
    /// </summary>
    public void ObserveReplica(string replicaId, IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> snapshot)
    {
        ValidateIdentifier(replicaId, nameof(replicaId));
        ArgumentNullException.ThrowIfNull(snapshot);

        var copy = new Dictionary<TKey, ConsistencyVersion<TKey>?>(_keyComparer);
        foreach (var pair in snapshot)
        {
            ArgumentNullException.ThrowIfNull(pair.Key);
            ValidateVersion(pair.Key, pair.Value, nameof(snapshot));
            if (pair.Value is not null)
            {
                copy.Add(pair.Key, pair.Value);
            }
        }

        VerifyReplicaSnapshot(replicaId, copy);
        _replicas[replicaId] = copy;
        _replicaObservations++;
        _scheduler.RecordTrace($"consistency:{Name}:replica:{replicaId}:observed:{copy.Count}");
        _scheduler.Coverage.RecordAutomatic($"consistency:{Name}:replica-observation");
        TryCompleteConvergenceRequirements();
    }

    /// <summary>
    /// Requires the selected replicas to expose exactly the same logical key versions within the supplied virtual-time bound.
    /// </summary>
    public void RequireConvergence(string name, TimeSpan within, IReadOnlyList<string> replicaIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(replicaIds);

        if (within < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(within), within, "Convergence requires a non-negative finite duration.");
        }

        if (replicaIds.Count < 2)
        {
            throw new ArgumentException("Convergence requires at least two replicas.", nameof(replicaIds));
        }

        var replicas = replicaIds.Select(ValidateReplicaForConvergence).Distinct(StringComparer.Ordinal).ToArray();
        if (replicas.Length != replicaIds.Count)
        {
            throw new ArgumentException("Convergence replica identifiers must be unique.", nameof(replicaIds));
        }

        if (!_convergenceNames.Add(name))
        {
            throw new InvalidOperationException($"A convergence requirement named '{name}' already exists in history '{Name}'.");
        }

        var registeredAt = _scheduler.TimeProvider.GetUtcNow();
        var registration = new ConsistencyConvergenceRegistration
        {
            Name = name,
            Replicas = replicas,
            RegisteredAt = registeredAt,
            Deadline = registeredAt.Add(within)
        };
        _convergence.Add(registration);
        _scheduler.RecordTrace($"consistency:{Name}:convergence:registered:{name}:deadline:{registration.Deadline.Ticks}");

        if (AreReplicasConverged(replicas))
        {
            CompleteConvergence(registration, registeredAt);
            return;
        }

        if (within == TimeSpan.Zero)
        {
            HandleConvergenceDeadline(registration);
            return;
        }

        _scheduler.TrackOperation(registration.Completion.Task);
        registration.DeadlineTimer = _scheduler.TimeProvider.CreateTimer(
            static state => ((Action)state!).Invoke(),
            () => HandleConvergenceDeadline(registration),
            within,
            Timeout.InfiniteTimeSpan);
    }

    ConsistencyOutcome IConsistencyHistory.Complete()
    {
        var incomplete = _convergence.FirstOrDefault(registration => !registration.IsCompleted);
        if (incomplete is not null)
        {
            throw new InvalidOperationException(
                $"Consistency convergence requirement '{incomplete.Name}' remained incomplete when history '{Name}' ended.");
        }

        var outcomes = _convergence
            .Select(
                registration => new ConsistencyConvergenceOutcome(
                    registration.Name,
                    registration.Replicas,
                    registration.RegisteredAt,
                    registration.CompletedAt!.Value))
            .ToArray();

        return new ConsistencyOutcome(Name, Guarantees, _versions.Count, _reads, _replicaObservations, outcomes);
    }

    private void VerifyReadYourWrites(string sessionId, TKey key, ConsistencyVersion<TKey>? observedVersion, ConsistencySessionState<TKey> session)
    {
        if (!Guarantees.HasFlag(ConsistencyGuarantees.ReadYourWrites) || !session.LastWrites.TryGetValue(key, out var required))
        {
            return;
        }

        if (!IsEqualOrDescendant(observedVersion, required))
        {
            ThrowViolation(
                ConsistencyViolationKind.ReadYourWrites,
                $"Session '{sessionId}' did not observe its write version {required.Id} for key '{key}'.",
                sessionId,
                null,
                key);
        }
    }

    private void VerifyMonotonicReads(string sessionId, TKey key, ConsistencyVersion<TKey>? observedVersion, ConsistencySessionState<TKey> session)
    {
        if (!Guarantees.HasFlag(ConsistencyGuarantees.MonotonicReads) || !session.LastReads.TryGetValue(key, out var previous))
        {
            return;
        }

        if (previous is not null && !IsEqualOrDescendant(observedVersion, previous))
        {
            ThrowViolation(
                ConsistencyViolationKind.MonotonicReads,
                $"Session '{sessionId}' regressed behind previously observed version {previous.Id} for key '{key}'.",
                sessionId,
                null,
                key);
        }
    }

    private void VerifyReplicaReadAgreement(string sessionId, string replicaId, TKey key, ConsistencyVersion<TKey>? observedVersion)
    {
        if (!Guarantees.HasFlag(ConsistencyGuarantees.ReplicaReadAgreement) || !_replicas.TryGetValue(replicaId, out var snapshot))
        {
            return;
        }

        snapshot.TryGetValue(key, out var expected);
        if (expected?.Id == observedVersion?.Id)
        {
            return;
        }

        ThrowViolation(
            ConsistencyViolationKind.ReplicaReadAgreement,
            $"Read from replica '{replicaId}' did not agree with its recorded snapshot for key '{key}'.",
            sessionId,
            replicaId,
            key);
    }

    private void VerifyReplicaSnapshot(string replicaId, IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> snapshot)
    {
        foreach (var visible in snapshot.Values.Where(version => version is not null).Cast<ConsistencyVersion<TKey>>())
        {
            if (Guarantees.HasFlag(ConsistencyGuarantees.MonotonicWrites))
            {
                VerifyMonotonicWrites(replicaId, snapshot, visible);
            }

            if (Guarantees.HasFlag(ConsistencyGuarantees.WritesFollowReads))
            {
                VerifyWritesFollowReads(replicaId, snapshot, visible);
            }

            if (Guarantees.HasFlag(ConsistencyGuarantees.CausalVisibility))
            {
                VerifyCausalVisibility(replicaId, snapshot, visible);
            }
        }
    }

    private void VerifyMonotonicWrites(string replicaId, IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> snapshot, ConsistencyVersion<TKey> visible)
    {
        foreach (var predecessor in _versions.Values.Where(
                     version => version.SessionId == visible.SessionId && version.SessionWriteSequence < visible.SessionWriteSequence))
        {
            if (SnapshotIncludes(snapshot, predecessor))
            {
                continue;
            }

            ThrowViolation(
                ConsistencyViolationKind.MonotonicWrites,
                $"Replica '{replicaId}' exposed version {visible.Id} without earlier session write {predecessor.Id}.",
                visible.SessionId,
                replicaId,
                predecessor.Key);
        }
    }

    private void VerifyWritesFollowReads(string replicaId, IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> snapshot, ConsistencyVersion<TKey> visible)
    {
        foreach (var predecessorId in visible.ReadPredecessorIds)
        {
            var predecessor = _versions[predecessorId];
            if (SnapshotIncludes(snapshot, predecessor))
            {
                continue;
            }

            ThrowViolation(
                ConsistencyViolationKind.WritesFollowReads,
                $"Replica '{replicaId}' exposed version {visible.Id} without previously read version {predecessor.Id}.",
                visible.SessionId,
                replicaId,
                predecessor.Key);
        }
    }

    private void VerifyCausalVisibility(string replicaId, IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> snapshot, ConsistencyVersion<TKey> visible)
    {
        foreach (var predecessorId in visible.CausalPredecessorIds)
        {
            var predecessor = _versions[predecessorId];
            if (SnapshotIncludes(snapshot, predecessor))
            {
                continue;
            }

            ThrowViolation(
                ConsistencyViolationKind.CausalVisibility,
                $"Replica '{replicaId}' exposed version {visible.Id} without causal predecessor {predecessor.Id}.",
                visible.SessionId,
                replicaId,
                predecessor.Key);
        }
    }

    private bool SnapshotIncludes(IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> snapshot, ConsistencyVersion<TKey> required)
    {
        return snapshot.TryGetValue(required.Key, out var current) && IsEqualOrDescendant(current, required);
    }

    private bool IsEqualOrDescendant(ConsistencyVersion<TKey>? candidate, ConsistencyVersion<TKey> ancestor)
    {
        return candidate is not null &&
            (candidate.Id == ancestor.Id || candidate.CausalPredecessorIds.Contains(ancestor.Id));
    }

    private void ValidateVersion(TKey key, ConsistencyVersion<TKey>? version, string parameterName)
    {
        if (version is null)
        {
            return;
        }

        if (!_versions.TryGetValue(version.Id, out var owned) || !ReferenceEquals(owned, version))
        {
            throw new ArgumentException("The consistency version belongs to another history.", parameterName);
        }

        if (!_keyComparer.Equals(version.Key, key))
        {
            throw new ArgumentException("The consistency version belongs to another logical key.", parameterName);
        }
    }

    private ConsistencySessionState<TKey> GetSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        session = new ConsistencySessionState<TKey>(_keyComparer);
        _sessions.Add(sessionId, session);
        return session;
    }

    private void AddCausalClosure(ConsistencySessionState<TKey> session, ConsistencyVersion<TKey> version)
    {
        session.CausalVersionIds.Add(version.Id);
        foreach (var predecessorId in version.CausalPredecessorIds)
        {
            session.CausalVersionIds.Add(predecessorId);
        }
    }

    private void TryCompleteConvergenceRequirements()
    {
        var now = _scheduler.TimeProvider.GetUtcNow();
        foreach (var registration in _convergence.Where(registration => !registration.IsCompleted))
        {
            if (AreReplicasConverged(registration.Replicas))
            {
                CompleteConvergence(registration, now);
            }
        }
    }

    private bool AreReplicasConverged(IReadOnlyList<string> replicaIds)
    {
        Dictionary<TKey, ConsistencyVersion<TKey>?>? baseline = null;
        foreach (var replicaId in replicaIds)
        {
            if (!_replicas.TryGetValue(replicaId, out var snapshot))
            {
                return false;
            }

            if (baseline is null)
            {
                baseline = snapshot;
                continue;
            }

            if (!SnapshotsEqual(baseline, snapshot))
            {
                return false;
            }
        }

        return baseline is not null;
    }

    private bool SnapshotsEqual(IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> left, IReadOnlyDictionary<TKey, ConsistencyVersion<TKey>?> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightVersion) || pair.Value?.Id != rightVersion?.Id)
            {
                return false;
            }
        }

        return true;
    }

    private void CompleteConvergence(ConsistencyConvergenceRegistration registration, DateTimeOffset completedAt)
    {
        if (registration.IsCompleted)
        {
            return;
        }

        registration.CompletedAt = completedAt;
        registration.DeadlineTimer?.Dispose();
        registration.Completion.TrySetResult();
        _scheduler.RecordTrace($"consistency:{Name}:convergence:satisfied:{registration.Name}");
        _scheduler.Coverage.RecordAutomatic($"consistency:{Name}:convergence");
    }

    private void HandleConvergenceDeadline(ConsistencyConvergenceRegistration registration)
    {
        if (registration.IsCompleted)
        {
            return;
        }

        var now = _scheduler.TimeProvider.GetUtcNow();
        if (AreReplicasConverged(registration.Replicas))
        {
            CompleteConvergence(registration, now);
            return;
        }

        _scheduler.RecordTrace($"consistency:{Name}:convergence:violated:{registration.Name}");
        ThrowViolation(
            ConsistencyViolationKind.Convergence,
            $"Replicas for convergence requirement '{registration.Name}' did not converge by {registration.Deadline:O}.",
            null,
            null,
            null);
    }

    private void ThrowViolation(ConsistencyViolationKind kind, string message, string? sessionId, string? replicaId, object? key)
    {
        _scheduler.RecordTrace($"consistency:{Name}:violated:{kind}");
        _scheduler.Coverage.RecordAutomatic($"consistency:{Name}:violation:{kind}");
        throw new SimulationConsistencyViolationException(Name, kind, message, sessionId, replicaId, key);
    }

    private static string ValidateReplicaForConvergence(string replicaId)
    {
        ValidateIdentifier(replicaId, nameof(replicaId));
        return replicaId;
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Consistency identifiers cannot be null, empty or whitespace.", parameterName);
        }
    }
}
