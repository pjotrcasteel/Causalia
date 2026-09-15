namespace Causalia.Storage.Exceptions;

/// <summary>
/// Represents a storage failure that happened before durable commit was known to complete.
/// </summary>
public sealed class SimulationStorageTransientException : SimulationStorageException
{
    internal SimulationStorageTransientException(string storageName, long operationId, string code)
        : base($"Storage '{storageName}' failed operation {operationId} before durable completion. Code={code}.", storageName, operationId, code)
    {
    }
}
