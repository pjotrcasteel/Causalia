namespace Causalia.Linearizability;

internal sealed record LinearizabilityOperationBoundary(long Sequence, DateTimeOffset Time);
