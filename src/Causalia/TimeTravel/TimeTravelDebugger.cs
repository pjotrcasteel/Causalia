namespace Causalia.TimeTravel;

/// <summary>
/// Provides cursor-based navigation and watched-state diffing across one retained timeline.
/// </summary>
public sealed class TimeTravelDebugger
{
    private readonly TimeTravelTimeline _timeline;
    private int _position;

    internal TimeTravelDebugger(TimeTravelTimeline timeline)
    {
        _timeline = timeline;
        _position = timeline.Checkpoints.Count - 1;
    }

    /// <summary>
    /// Gets the currently selected checkpoint when the timeline is not empty.
    /// </summary>
    public TimeTravelCheckpoint? Current => _position < 0 ? null : _timeline.Checkpoints[_position];

    /// <summary>
    /// Moves to the previous retained checkpoint when possible.
    /// </summary>
    public bool MovePrevious()
    {
        if (_position <= 0)
        {
            return false;
        }

        _position--;
        return true;
    }

    /// <summary>
    /// Moves to the next retained checkpoint when possible.
    /// </summary>
    public bool MoveNext()
    {
        if (_position < 0 || _position >= _timeline.Checkpoints.Count - 1)
        {
            return false;
        }

        _position++;
        return true;
    }

    /// <summary>
    /// Moves directly to one retained tt1 checkpoint token.
    /// </summary>
    public bool Seek(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        for (var index = 0; index < _timeline.Checkpoints.Count; index++)
        {
            if (!string.Equals(_timeline.Checkpoints[index].Token, token, StringComparison.Ordinal))
            {
                continue;
            }

            _position = index;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Moves to a checkpoint resolved relative to one scheduler step.
    /// </summary>
    public bool SeekStep(int step, TimeTravelSeekMode mode = TimeTravelSeekMode.AtOrBefore)
    {
        if (step < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "Scheduler step cannot be negative.");
        }

        var index = mode switch
        {
            TimeTravelSeekMode.AtOrBefore => FindAtOrBefore(step),
            TimeTravelSeekMode.AtOrAfter => FindAtOrAfter(step),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported time-travel seek mode.")
        };

        if (index < 0)
        {
            return false;
        }

        _position = index;
        return true;
    }

    /// <summary>
    /// Moves to a retained checkpoint immediately before or after one trace-event index.
    /// </summary>
    public bool SeekTraceEvent(int traceEventIndex, TimeTravelTraceSeekMode mode)
    {
        if (traceEventIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(traceEventIndex),
                traceEventIndex,
                "Trace-event index cannot be negative.");
        }

        var index = mode switch
        {
            TimeTravelTraceSeekMode.BeforeEvent => FindBeforeTraceEvent(traceEventIndex),
            TimeTravelTraceSeekMode.AfterEvent => FindAfterTraceEvent(traceEventIndex),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported trace-event seek mode.")
        };

        if (index < 0)
        {
            return false;
        }

        _position = index;
        return true;
    }

    /// <summary>
    /// Creates a diff between the previous retained checkpoint and the current checkpoint.
    /// </summary>
    public TimeTravelCheckpointDiff? DiffFromPrevious()
    {
        if (_position <= 0)
        {
            return null;
        }

        return _timeline.Diff(_timeline.Checkpoints[_position - 1], _timeline.Checkpoints[_position]);
    }

    /// <summary>
    /// Moves backwards to the nearest checkpoint where one watched state probe changed.
    /// </summary>
    public bool MoveToPreviousChange(string probeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(probeName);

        for (var index = _position; index > 0; index--)
        {
            if (!ProbeChanged(_timeline.Checkpoints[index - 1], _timeline.Checkpoints[index], probeName))
            {
                continue;
            }

            _position = index - 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Moves forwards to the nearest checkpoint where one watched state probe changed.
    /// </summary>
    public bool MoveToNextChange(string probeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(probeName);

        for (var index = Math.Max(_position + 1, 1); index < _timeline.Checkpoints.Count; index++)
        {
            if (!ProbeChanged(_timeline.Checkpoints[index - 1], _timeline.Checkpoints[index], probeName))
            {
                continue;
            }

            _position = index;
            return true;
        }

        return false;
    }

    private int FindBeforeTraceEvent(int traceEventIndex)
    {
        for (var index = _timeline.Checkpoints.Count - 1; index >= 0; index--)
        {
            if (_timeline.Checkpoints[index].TraceCount <= traceEventIndex)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindAfterTraceEvent(int traceEventIndex)
    {
        for (var index = 0; index < _timeline.Checkpoints.Count; index++)
        {
            if (_timeline.Checkpoints[index].TraceCount > traceEventIndex)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindAtOrBefore(int step)
    {
        for (var index = _timeline.Checkpoints.Count - 1; index >= 0; index--)
        {
            if (_timeline.Checkpoints[index].Step <= step)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindAtOrAfter(int step)
    {
        for (var index = 0; index < _timeline.Checkpoints.Count; index++)
        {
            if (_timeline.Checkpoints[index].Step >= step)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ProbeChanged(TimeTravelCheckpoint before, TimeTravelCheckpoint after, string probeName)
    {
        var left = before.FindState(probeName);
        var right = after.FindState(probeName);

        if (left is null || right is null)
        {
            return left is not null || right is not null;
        }

        return !string.Equals(left.Value, right.Value, StringComparison.Ordinal) ||
               !string.Equals(left.ErrorType, right.ErrorType, StringComparison.Ordinal) ||
               !string.Equals(left.ErrorMessage, right.ErrorMessage, StringComparison.Ordinal);
    }
}
