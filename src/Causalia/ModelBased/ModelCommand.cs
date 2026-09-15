namespace Causalia.ModelBased;

/// <summary>
/// Defines one state-machine command that transitions the model, executes against the system under test and verifies the observation.
/// </summary>
public sealed class ModelCommand<TState, TSystem>
{
    private readonly Func<TState, bool> _isEnabled;
    private readonly Func<TState, TState> _transition;
    private readonly Func<TSystem, SimulationContext, CancellationToken, Task<object?>> _executeAsync;
    private readonly Func<TState, TState, object?, ModelCommandVerification> _verify;

    internal ModelCommand(
        string name,
        Func<TState, bool> isEnabled,
        Func<TState, TState> transition,
        Func<TSystem, SimulationContext, CancellationToken, Task<object?>> executeAsync,
        Func<TState, TState, object?, ModelCommandVerification> verify)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(isEnabled);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(verify);
        Name = name;
        _isEnabled = isEnabled;
        _transition = transition;
        _executeAsync = executeAsync;
        _verify = verify;
    }

    /// <summary>
    /// Gets the stable command name used in model-sequence replay tokens.
    /// </summary>
    public string Name { get; }

    internal bool IsEnabled(TState state)
    {
        return _isEnabled(state);
    }

    internal TState Transition(TState state)
    {
        return _transition(state);
    }

    internal async Task<ModelCommandVerification> ExecuteAndVerifyAsync(
        TState before,
        TState expected,
        TSystem system,
        SimulationContext context,
        CancellationToken cancellationToken)
    {
        var observation = await _executeAsync(system, context, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return _verify(before, expected, observation);
    }
}
