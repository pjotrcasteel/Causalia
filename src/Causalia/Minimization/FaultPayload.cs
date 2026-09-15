namespace Causalia.Minimization;

internal sealed record FaultPayload(string Scope, long InjectorId, long Occurrence, string PolicyName);
