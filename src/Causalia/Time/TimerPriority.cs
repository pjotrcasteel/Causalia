namespace Causalia.Time;

internal readonly struct TimerPriority : IComparable<TimerPriority>
{
    public TimerPriority(long dueTimestamp, long sequence)
    {
        DueTimestamp = dueTimestamp;
        Sequence = sequence;
    }

    public long DueTimestamp { get; }

    public long Sequence { get; }

    public int CompareTo(TimerPriority other)
    {
        var dueComparison = DueTimestamp.CompareTo(other.DueTimestamp);
        return dueComparison != 0 ? dueComparison : Sequence.CompareTo(other.Sequence);
    }
}
