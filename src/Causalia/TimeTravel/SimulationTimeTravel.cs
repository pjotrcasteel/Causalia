using Causalia.Runtime;

namespace Causalia.TimeTravel;

/// <summary>
/// Registers deterministic state probes and captures immutable debugger checkpoints.
/// </summary>
public sealed class SimulationTimeTravel
{
    private readonly List<TimeTravelCheckpoint> _checkpoints = new();
    private readonly TimeTravelOptions _options;
    private readonly SortedDictionary<string, Func<string>> _probes = new(StringComparer.Ordinal);
    private readonly DeterministicScheduler _scheduler;
    private long _capturedCheckpointCount;
    private long _droppedCheckpointCount;

    internal SimulationTimeTravel(DeterministicScheduler scheduler, TimeTravelOptions options)
    {
        _scheduler = scheduler;
        _options = options;
    }

    /// <summary>
    /// Registers a stable textual state probe that will be evaluated at subsequent checkpoints.
    /// Probe exceptions are recorded as snapshot data and never alter simulation behavior.
    /// </summary>
    public void Watch(string name, Func<string> capture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(capture);

        if (!_probes.TryAdd(name, capture))
        {
            throw new InvalidOperationException($"A time-travel state probe named '{name}' already exists.");
        }
    }

    /// <summary>
    /// Registers a typed state probe with an explicit deterministic formatter.
    /// </summary>
    public void Watch<T>(string name, Func<T> capture, Func<T, string> formatter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(formatter);
        Watch(name, () => formatter(capture()));
    }

    /// <summary>
    /// Captures one explicit checkpoint and returns its stable tt1 token.
    /// </summary>
    public string Checkpoint(string? label = null)
    {
        if (label is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
        }

        return Capture(TimeTravelCheckpointKind.Manual, label).Token;
    }

    internal void CaptureAutomatic(TimeTravelCheckpointKind kind)
    {
        if (_options.CaptureMode != TimeTravelCaptureMode.SchedulerSteps)
        {
            return;
        }

        Capture(kind, null);
    }

    internal TimeTravelTimeline CreateTimeline()
    {
        return new TimeTravelTimeline(
            new List<TimeTravelCheckpoint>(_checkpoints).AsReadOnly(),
            _capturedCheckpointCount,
            _droppedCheckpointCount);
    }

    private TimeTravelCheckpoint Capture(TimeTravelCheckpointKind kind, string? label)
    {
        var index = _capturedCheckpointCount;
        _capturedCheckpointCount++;
        var state = CaptureState();
        var checkpoint = new TimeTravelCheckpoint(
            new TimeTravelCheckpointData
            {
                Index = index,
                Step = _scheduler.StepCount,
                Timestamp = _scheduler.TimeProvider.GetUtcNow(),
                Kind = kind,
                Label = label,
                TraceCount = _scheduler.Trace.Count,
                State = state
            });
        Retain(checkpoint);
        return checkpoint;
    }

    private IReadOnlyList<TimeTravelProbeSnapshot> CaptureState()
    {
        if (_probes.Count == 0)
        {
            return Array.Empty<TimeTravelProbeSnapshot>();
        }

        var state = new List<TimeTravelProbeSnapshot>(_probes.Count);

        foreach (var probe in _probes)
        {
            try
            {
                state.Add(new TimeTravelProbeSnapshot(probe.Key, probe.Value(), null, null));
            }
            catch (Exception exception)
            {
                state.Add(
                    new TimeTravelProbeSnapshot(
                        probe.Key,
                        null,
                        exception.GetType().FullName ?? exception.GetType().Name,
                        exception.Message));
            }
        }

        return state.AsReadOnly();
    }

    private void Retain(TimeTravelCheckpoint checkpoint)
    {
        if (_checkpoints.Count == _options.MaxRetainedCheckpoints)
        {
            _checkpoints.RemoveAt(0);
            _droppedCheckpointCount++;
        }

        _checkpoints.Add(checkpoint);
    }
}
