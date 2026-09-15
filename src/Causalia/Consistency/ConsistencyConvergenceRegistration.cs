namespace Causalia.Consistency;

/// <summary>
/// Tracks one pending bounded replica-convergence requirement.
/// </summary>
internal sealed class ConsistencyConvergenceRegistration
{
    public required string Name { get; init; }

    public required IReadOnlyList<string> Replicas { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }

    public required DateTimeOffset Deadline { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }

    public TaskCompletionSource Completion { get; } = new();

    public ITimer? DeadlineTimer { get; set; }

    public bool IsCompleted => CompletedAt.HasValue;
}
