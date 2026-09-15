namespace Causalia.Storage.Internal;

internal sealed class StagedStorageChange
{
    public StagedStorageChange(byte[]? value, long? expectedVersion)
    {
        Value = value;
        ExpectedVersion = expectedVersion;
    }

    public byte[]? Value { get; }

    public long? ExpectedVersion { get; }
}
