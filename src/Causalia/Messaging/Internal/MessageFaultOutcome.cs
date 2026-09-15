namespace Causalia.Messaging.Internal;

internal sealed record MessageFaultOutcome(bool Drop, bool Reorder, int AdditionalCopies, TimeSpan AdditionalDelay);
