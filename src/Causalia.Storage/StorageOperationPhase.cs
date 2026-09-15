namespace Causalia.Storage;

/// <summary>
/// Identifies the deterministic boundary around a durable-storage operation.
/// </summary>
public enum StorageOperationPhase
{
    /// <summary>
    /// Runs before a durable read is evaluated.
    /// </summary>
    BeforeRead,

    /// <summary>
    /// Runs after a durable read has been evaluated.
    /// </summary>
    AfterRead,

    /// <summary>
    /// Runs before a durable write is applied.
    /// </summary>
    BeforeWrite,

    /// <summary>
    /// Runs after a durable write has been applied.
    /// </summary>
    AfterWrite,

    /// <summary>
    /// Runs before a durable delete is applied.
    /// </summary>
    BeforeDelete,

    /// <summary>
    /// Runs after a durable delete has been applied.
    /// </summary>
    AfterDelete,

    /// <summary>
    /// Runs before an atomic transaction commit is applied.
    /// </summary>
    BeforeCommit,

    /// <summary>
    /// Runs after an atomic transaction commit has been durably applied.
    /// </summary>
    AfterCommit
}
