namespace Causalia.Storage.Exceptions;

/// <summary>
/// Represents a failure observed after the durable commit completed, leaving the caller uncertain about the outcome.
/// </summary>
public sealed class SimulationStorageAmbiguousCommitException : SimulationStorageException
{
    internal SimulationStorageAmbiguousCommitException(string storageName, long operationId, string code)
        : base($"Storage '{storageName}' committed operation {operationId}, but acknowledgement failed. Code={code}.", storageName, operationId, code)
    {
    }
}
