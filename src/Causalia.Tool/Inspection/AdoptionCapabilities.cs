namespace Causalia.Tool.Inspection;

internal sealed class AdoptionCapabilities
{
    public bool UsesTimeProvider { get; init; }

    public bool UsesCancellationToken { get; init; }

    public bool UsesRetryMechanisms { get; init; }
}
