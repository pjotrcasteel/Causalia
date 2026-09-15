namespace Causalia.ModelBased;

/// <summary>
/// Creates strongly typed model commands while keeping heterogeneous observation types out of the public command collection.
/// </summary>
public static class ModelCommand
{
    /// <summary>
    /// Creates a model command with a typed observation and optional state precondition.
    /// </summary>
    public static ModelCommand<TState, TSystem> Create<TState, TSystem, TObservation>(
        string name,
        Func<TState, TState> transition,
        Func<TSystem, SimulationContext, CancellationToken, Task<TObservation>> executeAsync,
        Func<TState, TState, TObservation, ModelCommandVerification> verify,
        Func<TState, bool>? precondition = null)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(verify);

        return new ModelCommand<TState, TSystem>(
            name,
            precondition ?? (static _ => true),
            transition,
            async (system, context, cancellationToken) => await executeAsync(system, context, cancellationToken),
            (before, expected, observation) => verify(before, expected, (TObservation)observation!));
    }
}
