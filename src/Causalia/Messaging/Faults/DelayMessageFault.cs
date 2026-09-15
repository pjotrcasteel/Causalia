namespace Causalia.Messaging.Faults;

/// <summary>
/// Adds virtual latency to the logical message.
/// </summary>
public sealed record DelayMessageFault : MessageFault
{
    /// <summary>
    /// Initializes an additional-delay fault.
    /// </summary>
    public DelayMessageFault(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        Delay = delay;
    }

    /// <summary>
    /// Gets the additional virtual delay.
    /// </summary>
    public TimeSpan Delay { get; }
}
