namespace Causalia.Messaging.Faults;

/// <summary>
/// Adds one or more duplicate deliveries for the same logical message identifier.
/// </summary>
public sealed record DuplicateMessageFault : MessageFault
{
    /// <summary>
    /// Initializes a duplicate-delivery fault.
    /// </summary>
    public DuplicateMessageFault(int additionalCopies)
    {
        if (additionalCopies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalCopies), additionalCopies, "AdditionalCopies must be greater than zero.");
        }

        AdditionalCopies = additionalCopies;
    }

    /// <summary>
    /// Gets the number of duplicate deliveries in addition to the original delivery.
    /// </summary>
    public int AdditionalCopies { get; }
}
