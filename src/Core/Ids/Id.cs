using System;
using System.Security.Cryptography;
using System.Threading;

namespace PragmaStack.Core.Ids;

public readonly struct Id
    : IEquatable<Id>
{
    [ThreadStatic]
    private static long _lastTimestamp;
    [ThreadStatic]
    private static long _counter;

    // Shared fields for global monotonic ID generation
    // We use 2 separate fields because packing timestamp (42+ bits) + counter (26 bits) exceeds 64 bits
    private static readonly Lock _globalLock = new();
    private static long _globalTimestamp;
    private static long _globalCounter;

    // Properties
    public Guid Value { get; }

    // Constructors
    private Id(Guid value)
    {
        Value = value;
    }

    // Public Methods
    /// <summary>
    /// Generates a new monotonic UUIDv7 with per-thread sequential guarantee.
    /// This is the fastest method and guarantees IDs are sequential within the same thread.
    /// </summary>
    public static Id GenerateNewId()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Check if we're in a new millisecond
        if (timestamp > _lastTimestamp)
        {
            _lastTimestamp = timestamp;
            // Start counter at 0 for new millisecond to ensure monotonicity
            _counter = 0;
        }
        else if (timestamp < _lastTimestamp)
        {
            // Clock moved backwards - use last timestamp and increment counter
            timestamp = _lastTimestamp;
            _counter++;
        }
        else
        {
            // Same millisecond - increment counter
            _counter++;

            // Handle overflow (extremely unlikely with 26-bit counter = 67M IDs per ms)
            if (_counter > 0x3FFFFFF)
            {
                // Spin-wait for next millisecond
                SpinWaitForNextMillisecond(ref timestamp, ref _lastTimestamp);
                _counter = 0;
            }
        }

        return new Id(BuildUuidV7WithRandom(timestamp, _counter));
    }

    /// <summary>
    /// Generates a new monotonic UUIDv7 with global sequential guarantee across all threads.
    /// Slightly slower than GenerateNewId() but guarantees total ordering.
    /// </summary>
    public static Id GenerateNewGlobalId()
    {
        // Use System.Threading.Lock for better performance (25-30% faster than lock(object))
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long counter;

        // Atomically update timestamp and counter
        using (_globalLock.EnterScope())
        {
            long currentTs = Volatile.Read(ref _globalTimestamp);
            long currentCounter = Volatile.Read(ref _globalCounter);

            if (timestamp > currentTs)
            {
                // New millisecond - reset counter
                _globalTimestamp = timestamp;
                _globalCounter = 0;
                counter = 0;
            }
            else if (timestamp < currentTs)
            {
                // Clock moved backwards - use last timestamp and increment counter
                timestamp = currentTs;
                counter = currentCounter + 1;
                _globalCounter = counter;
            }
            else
            {
                // Same millisecond - increment counter
                counter = currentCounter + 1;
                _globalCounter = counter;

                // Handle overflow (67M IDs per millisecond should be enough!)
                if (counter > 0x3FFFFFF)
                {
                    // Spin-wait for next millisecond
                    while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() == timestamp)
                    {
                        Thread.SpinWait(100);
                    }
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _globalTimestamp = timestamp;
                    _globalCounter = 0;
                    counter = 0;
                }
            }
        }

        return new Id(BuildUuidV7Deterministic(timestamp, counter));
    }

    public static Id FromGuid(Guid guid)
    {
        return new Id(guid);
    }

    // Private Helper Methods
    /// <summary>
    /// Builds a UUIDv7 with random bytes for per-thread uniqueness while maintaining thread-local ordering.
    /// </summary>
    private static Guid BuildUuidV7WithRandom(long timestamp, long counter)
    {
        // Extract timestamp parts (48 bits total)
        var timestampHigh = (int)(timestamp >> 16);
        var timestampLow = (short)(timestamp & 0xFFFF);

        // Version 7 (4 bits) + first 12 bits of counter
        var versionAndCounter = (short)(0x7000 | ((counter >> 14) & 0x0FFF));

        // Variant (2 bits = 10) + next 6 bits of counter
        var variantHigh = (byte)(0x80 | ((counter >> 8) & 0x3F));

        // Remaining 8 bits of counter
        var counterLow = (byte)(counter & 0xFF);

        // Random bytes for uniqueness across threads
        Span<byte> randomBytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(randomBytes);

        return new Guid(
            a: timestampHigh,
            b: timestampLow,
            c: versionAndCounter,
            d: variantHigh,
            e: counterLow,
            f: randomBytes[0],
            g: randomBytes[1],
            h: randomBytes[2],
            i: randomBytes[3],
            j: randomBytes[4],
            k: randomBytes[5]
        );
    }

    /// <summary>
    /// Builds a UUIDv7 with deterministic bytes for global sequential ordering.
    /// Uses a 26-bit counter distributed across available random bits after version and variant.
    /// Counter layout: 12 bits in rand_a + 14 bits in upper rand_b = 26 bits total
    /// </summary>
    private static Guid BuildUuidV7Deterministic(long timestamp, long counter)
    {
        // Extract timestamp parts (48 bits total)
        var timestampHigh = (int)(timestamp >> 16);
        var timestampLow = (short)(timestamp & 0xFFFF);

        // Version 7 (4 bits) + counter bits 14-25 (12 bits)
        var versionAndCounter = (short)(0x7000 | ((counter >> 14) & 0x0FFF));

        // Variant (2 bits = 10) + counter bits 8-13 (6 bits)
        var variantHigh = (byte)(0x80 | ((counter >> 8) & 0x3F));

        // Counter bits 0-7 (8 bits)
        var byte1 = (byte)(counter & 0xFF);

        // Rest are zeros for deterministic ordering
        return new Guid(
            timestampHigh,      // int (4 bytes)
            timestampLow,       // short (2 bytes)
            versionAndCounter,  // short (2 bytes) - version + 12 bits counter
            variantHigh,        // byte 1 - variant + 6 bits counter
            byte1,              // byte 2 - 8 bits counter
            0,                  // byte 3 - zero
            0,                  // byte 4 - zero
            0,                  // byte 5 - zero
            0,                  // byte 6 - zero
            0,                  // byte 7 - zero
            0                   // byte 8 - zero
        );
    }

    /// <summary>
    /// Spin-waits until the next millisecond tick.
    /// </summary>
    private static void SpinWaitForNextMillisecond(ref long timestamp, ref long lastTimestamp)
    {
        while (timestamp == lastTimestamp)
        {
            Thread.SpinWait(100);
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        lastTimestamp = timestamp;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
    public override bool Equals(object? obj)
    {
        if (obj is not Id id)
            return false;

        return Equals(id);
    }
    public bool Equals(Id other)
    {
        return Value == other.Value;
    }

    // Operators
    public static implicit operator Guid(Id id) => id.Value;
    public static implicit operator Id(Guid guid) => FromGuid(guid);

    public static bool operator ==(Id left, Id right) => left.Value == right.Value;
    public static bool operator !=(Id left, Id right) => left.Value != right.Value;
    public static bool operator <(Id left, Id right) => left.Value.CompareTo(right.Value) < 0;
    public static bool operator >(Id left, Id right) => left.Value.CompareTo(right.Value) > 0;
    public static bool operator <=(Id left, Id right) => left.Value.CompareTo(right.Value) <= 0;
    public static bool operator >=(Id left, Id right) => left.Value.CompareTo(right.Value) >= 0;
}
