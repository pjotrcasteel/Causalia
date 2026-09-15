namespace Causalia.Storage.Internal;

internal sealed class StoredValue
{
    public StoredValue(byte[] value, long version)
    {
        Value = value;
        Version = version;
    }

    public byte[] Value { get; }

    public long Version { get; }
}
