using Causalia.Storage;

namespace Causalia.EntityFrameworkCore;

internal sealed class StorageOperationLeaseHolder
{
    public StorageOperationLeaseHolder(StorageOperationLease lease)
    {
        Lease = lease;
    }

    public StorageOperationLease Lease { get; }
}
