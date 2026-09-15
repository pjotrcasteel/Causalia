namespace Causalia.Storage;

/// <summary>
/// Identifies the logical durable-storage operation being simulated.
/// </summary>
public enum StorageOperationKind
{
    /// <summary>
    /// Reads durable state.
    /// </summary>
    Read,

    /// <summary>
    /// Writes durable state.
    /// </summary>
    Write,

    /// <summary>
    /// Deletes durable state.
    /// </summary>
    Delete,

    /// <summary>
    /// Atomically commits staged transaction changes.
    /// </summary>
    Commit
}
