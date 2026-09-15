namespace Causalia.ModelBased;

/// <summary>
/// Reports an observed system-under-test result that does not satisfy the executable model transition.
/// </summary>
public sealed class SimulationModelViolationException : Exception
{
    internal SimulationModelViolationException(
        string modelName,
        string commandName,
        int stepIndex,
        ModelSequence sequence,
        string message)
        : base($"Model '{modelName}' command '{commandName}' failed at step {stepIndex}: {message}")
    {
        ModelName = modelName;
        CommandName = commandName;
        StepIndex = stepIndex;
        Sequence = sequence;
        ViolationMessage = message;
    }

    /// <summary>
    /// Gets the model name.
    /// </summary>
    public string ModelName { get; }

    /// <summary>
    /// Gets the failing command name.
    /// </summary>
    public string CommandName { get; }

    /// <summary>
    /// Gets the zero-based failing command index.
    /// </summary>
    public int StepIndex { get; }

    /// <summary>
    /// Gets the command prefix through and including the failing command.
    /// </summary>
    public ModelSequence Sequence { get; }

    /// <summary>
    /// Gets the verifier-provided mismatch description.
    /// </summary>
    public string ViolationMessage { get; }
}
