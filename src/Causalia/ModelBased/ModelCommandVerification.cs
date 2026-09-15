namespace Causalia.ModelBased;

/// <summary>
/// Describes whether an observed system-under-test result matches the model expectation for one command.
/// </summary>
public readonly record struct ModelCommandVerification
{
    private ModelCommandVerification(bool succeeded, string? message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>
    /// Gets whether the command observation matched the model expectation.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the mismatch description when verification failed.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Creates a successful verification result.
    /// </summary>
    public static ModelCommandVerification Pass()
    {
        return new ModelCommandVerification(true, null);
    }

    /// <summary>
    /// Creates a failed verification result with a deterministic diagnostic message.
    /// </summary>
    public static ModelCommandVerification Fail(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ModelCommandVerification(false, message);
    }

    /// <summary>
    /// Compares an expected value with an observed value by using the supplied comparer or the default comparer.
    /// </summary>
    public static ModelCommandVerification Equal<T>(T expected, T actual, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        return comparer.Equals(expected, actual)
            ? Pass()
            : Fail($"Expected '{expected}', but observed '{actual}'.");
    }
}
