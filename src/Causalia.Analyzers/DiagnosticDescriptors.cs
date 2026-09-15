using Microsoft.CodeAnalysis;

namespace Causalia.Analyzers;

internal static class DiagnosticDescriptors
{
    private const string Category = "Causalia.Determinism";

    public static readonly DiagnosticDescriptor VirtualTime = new(
        "CAU1001",
        "Use Causalia virtual time",
        "'{0}' bypasses Causalia virtual time in deterministic simulation code; {1}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Wall-clock time, real timers and real delays make deterministic simulation replay unreliable.");

    public static readonly DiagnosticDescriptor Randomness = new(
        "CAU1002",
        "Use Causalia deterministic randomness",
        "'{0}' introduces nondeterministic or runtime-dependent randomness in deterministic simulation code; {1}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Simulation randomness should come from SimulationContext.Random so replay does not perturb scheduler or fault streams.");

    public static readonly DiagnosticDescriptor Concurrency = new(
        "CAU1003",
        "Avoid uncontrolled concurrency",
        "'{0}' escapes Causalia's deterministic scheduler; {1}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Raw threads, thread-pool work and Task.Run are outside Causalia's cooperative deterministic scheduler.");

    public static readonly DiagnosticDescriptor BlockingWait = new(
        "CAU1004",
        "Avoid blocking waits in deterministic simulation code",
        "'{0}' blocks the cooperative simulation thread; {1}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Blocking waits prevent Causalia from running the continuations or timers required to make progress.");

    public static readonly DiagnosticDescriptor ScenarioMethod = new(
        "CAU1005",
        "Mark scenario method for deterministic analysis",
        "'{0}' is passed as a Causalia simulation scenario but is not marked [DeterministicSimulation]; {1}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Method-group scenarios must be marked so Causalia can analyze their method bodies for deterministic escapes.");

    public static readonly DiagnosticDescriptor EscapedAwait = new(
        "CAU1006",
        "Keep awaits on the deterministic scheduler",
        "'{0}' disables synchronization-context capture in deterministic simulation code; {1}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ConfigureAwait(false) moves continuations outside Causalia's cooperative deterministic scheduler.");
}
