namespace Causalia.ModelBased;

/// <summary>
/// Defines an executable state-machine model and how a fresh system under test is created for each explored command sequence.
/// </summary>
public sealed class ModelBasedSpecification<TState, TSystem>
{
    private readonly Func<TState> _initialStateFactory;
    private readonly Func<SimulationContext, CancellationToken, Task<TSystem>> _systemFactory;
    private readonly Func<TState, IReadOnlyList<ModelCommand<TState, TSystem>>> _commands;

    /// <summary>
    /// Initializes a model-based specification.
    /// </summary>
    public ModelBasedSpecification(
        string name,
        Func<TState> initialStateFactory,
        Func<SimulationContext, CancellationToken, Task<TSystem>> systemFactory,
        Func<TState, IReadOnlyList<ModelCommand<TState, TSystem>>> commands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(initialStateFactory);
        ArgumentNullException.ThrowIfNull(systemFactory);
        ArgumentNullException.ThrowIfNull(commands);
        Name = name;
        _initialStateFactory = initialStateFactory;
        _systemFactory = systemFactory;
        _commands = commands;
    }

    /// <summary>
    /// Gets the stable model name used in diagnostics and trace events.
    /// </summary>
    public string Name { get; }

    internal TState CreateInitialState()
    {
        return _initialStateFactory();
    }

    internal Task<TSystem> CreateSystemAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        return _systemFactory(context, cancellationToken)
            ?? throw new InvalidOperationException($"Model '{Name}' system factory returned a null task.");
    }

    internal IReadOnlyList<ModelCommand<TState, TSystem>> GetEnabledCommands(TState state)
    {
        var commands = _commands(state)
            ?? throw new InvalidOperationException($"Model '{Name}' command provider returned null.");
        var enabled = new List<ModelCommand<TState, TSystem>>(commands.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var command in commands)
        {
            if (command is null)
            {
                throw new InvalidOperationException($"Model '{Name}' command provider returned a null command.");
            }

            if (!names.Add(command.Name))
            {
                throw new InvalidOperationException(
                    $"Model '{Name}' exposed duplicate command name '{command.Name}' for the same state.");
            }

            if (command.IsEnabled(state))
            {
                enabled.Add(command);
            }
        }

        return enabled.AsReadOnly();
    }
}
