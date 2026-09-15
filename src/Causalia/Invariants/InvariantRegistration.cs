namespace Causalia.Invariants;

internal sealed class InvariantRegistration
{
    public InvariantRegistration(
        string name,
        InvariantKind kind,
        Func<bool> predicate,
        DateTimeOffset registeredAt,
        DateTimeOffset? deadline)
    {
        Name = name;
        Kind = kind;
        Predicate = predicate;
        RegisteredAt = registeredAt;
        Deadline = deadline;
    }

    public string Name { get; }

    public InvariantKind Kind { get; }

    public Func<bool> Predicate { get; }

    public DateTimeOffset RegisteredAt { get; }

    public DateTimeOffset? Deadline { get; }

    public DateTimeOffset? CompletedAt { get; set; }

    public ITimer? DeadlineTimer { get; set; }

    public TaskCompletionSource? Completion { get; set; }

    public bool IsCompleted => CompletedAt.HasValue;
}
