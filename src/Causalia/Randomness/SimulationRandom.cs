using System.Buffers.Binary;

namespace Causalia.Randomness;

/// <summary>
/// Provides deterministic user-facing randomness without perturbing scheduler or fault random streams.
/// </summary>
public sealed class SimulationRandom
{
    private readonly DeterministicRandom _random;

    internal SimulationRandom(ulong seed)
    {
        _random = new DeterministicRandom(seed);
    }

    /// <summary>
    /// Returns a deterministic integer greater than or equal to zero and less than the exclusive maximum.
    /// </summary>
    public int NextInt32(int exclusiveMaximum)
    {
        return _random.NextInt32(exclusiveMaximum);
    }

    /// <summary>
    /// Returns a deterministic integer in the requested half-open range.
    /// </summary>
    public int NextInt32(int inclusiveMinimum, int exclusiveMaximum)
    {
        if (inclusiveMinimum >= exclusiveMaximum)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum), "exclusiveMaximum must be greater than inclusiveMinimum.");
        }

        var range = checked(exclusiveMaximum - inclusiveMinimum);
        return checked(inclusiveMinimum + _random.NextInt32(range));
    }

    /// <summary>
    /// Returns the next deterministic unsigned 64-bit value.
    /// </summary>
    public ulong NextUInt64()
    {
        return _random.NextUInt64();
    }

    /// <summary>
    /// Returns a deterministic Boolean value.
    /// </summary>
    public bool NextBoolean()
    {
        return (_random.NextUInt64() & 1UL) != 0;
    }

    /// <summary>
    /// Returns a deterministic floating-point value greater than or equal to zero and less than one.
    /// </summary>
    public double NextDouble()
    {
        const double denominator = 1d / (1UL << 53);
        return (_random.NextUInt64() >> 11) * denominator;
    }

    /// <summary>
    /// Fills the destination with deterministic pseudo-random bytes.
    /// </summary>
    public void Fill(Span<byte> destination)
    {
        var offset = 0;

        while (destination.Length - offset >= sizeof(ulong))
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], _random.NextUInt64());
            offset += sizeof(ulong);
        }

        if (offset == destination.Length)
        {
            return;
        }

        Span<byte> remainder = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(remainder, _random.NextUInt64());
        remainder[..(destination.Length - offset)].CopyTo(destination[offset..]);
    }

    /// <summary>
    /// Returns a deterministic RFC 4122 variant GUID with version-4 layout bits.
    /// </summary>
    public Guid NextGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        Fill(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
