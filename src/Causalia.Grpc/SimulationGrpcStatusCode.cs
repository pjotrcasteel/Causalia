namespace Causalia.Grpc;

/// <summary>
/// Represents canonical gRPC status codes used by deterministic simulated calls.
/// </summary>
public enum SimulationGrpcStatusCode
{
    /// <summary>Success.</summary>
    Ok,
    /// <summary>Operation cancelled.</summary>
    Cancelled,
    /// <summary>Unknown failure.</summary>
    Unknown,
    /// <summary>Invalid request argument.</summary>
    InvalidArgument,
    /// <summary>Deadline exceeded.</summary>
    DeadlineExceeded,
    /// <summary>Requested resource not found.</summary>
    NotFound,
    /// <summary>Resource already exists.</summary>
    AlreadyExists,
    /// <summary>Permission denied.</summary>
    PermissionDenied,
    /// <summary>Resource exhausted.</summary>
    ResourceExhausted,
    /// <summary>Failed precondition.</summary>
    FailedPrecondition,
    /// <summary>Operation aborted.</summary>
    Aborted,
    /// <summary>Value outside valid range.</summary>
    OutOfRange,
    /// <summary>Operation not implemented.</summary>
    Unimplemented,
    /// <summary>Internal failure.</summary>
    Internal,
    /// <summary>Service unavailable.</summary>
    Unavailable,
    /// <summary>Unrecoverable data loss.</summary>
    DataLoss,
    /// <summary>Unauthenticated caller.</summary>
    Unauthenticated
}
