// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
#if !NET && !NETSTANDARD2_1_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace OpenTelemetry.Metrics;

internal readonly struct Tags : IEquatable<Tags>
{
    public static readonly Tags EmptyTags = new([]);

    // A per-process random seed for hashing tag values and deriving type-specific seeds.
    private static readonly ulong HashSeed = GenerateHashSeed();

    private static readonly ulong Int32HashSeed = GetTypeHashSeed(typeof(int));
    private static readonly ulong UInt32HashSeed = GetTypeHashSeed(typeof(uint));
    private static readonly ulong SingleHashSeed = GetTypeHashSeed(typeof(float));
    private static readonly ulong CustomHashSeed = GetTypeHashSeed(typeof(object));

    // Reserve zero for null and distinct low bits for the two Boolean hashes.
    private static readonly int FalseHashCode = (GetSeededScalarHashCode<bool>(0) & ~3) | 1;
    private static readonly int TrueHashCode = (GetSeededScalarHashCode<bool>(1) & ~3) | 2;

    private readonly int hashCode;

    public Tags(KeyValuePair<string, object?>[] keyValuePairs)
    {
        this.KeyValuePairs = keyValuePairs;
        this.hashCode = ComputeHashCode(keyValuePairs);
    }

    public readonly KeyValuePair<string, object?>[] KeyValuePairs { get; }

    public static bool operator ==(Tags tag1, Tags tag2) => tag1.Equals(tag2);

    public static bool operator !=(Tags tag1, Tags tag2) => !tag1.Equals(tag2);

    public override readonly bool Equals(object? obj)
        => obj is Tags other && this.Equals(other);

    public readonly bool Equals(Tags other)
    {
        var ourKvps = this.KeyValuePairs;
        var theirKvps = other.KeyValuePairs;

        if (ReferenceEquals(ourKvps, theirKvps))
        {
            return true;
        }

        if (this.hashCode != other.hashCode)
        {
            return false;
        }

        return SequenceEqual(ourKvps, theirKvps);
    }

    public readonly bool Equals(ReadOnlySpan<KeyValuePair<string, object?>> other)
        => SequenceEqual(this.KeyValuePairs, other);

    public override readonly int GetHashCode() => this.hashCode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ComputeHashCode(ReadOnlySpan<KeyValuePair<string, object?>> keyValuePairs)
    {
        // Every entry in a lookup dictionary belongs to a single metric stream, so
        // the entries (almost always) share the same tag keys, and those keys are
        // almost always the same string instances (literals or cached constants)
        // on every measurement. Hashing each key string on every lookup is
        // therefore repeated work: the key hashes are served from a per-thread
        // cache keyed by string reference instead (see GetKeyHashCode), while the
        // values, which carry the entropy, are hashed in full.
        var keyHashCache = ThreadStaticStorage.GetStorage().KeyHashCache;
#if NET || NETSTANDARD2_1_OR_GREATER
        // The fixed-arity overloads avoid incremental hashing overhead for short tag sets.
        if (keyValuePairs.Length == 1)
        {
            ref readonly var item = ref keyValuePairs[0];
            return HashCode.Combine(GetKeyHashCode(keyHashCache, item.Key), GetValueHashCode(item.Value));
        }

        if (keyValuePairs.Length == 2)
        {
            ref readonly var first = ref keyValuePairs[0];
            ref readonly var second = ref keyValuePairs[1];
            return HashCode.Combine(
                GetKeyHashCode(keyHashCache, first.Key),
                GetValueHashCode(first.Value),
                GetKeyHashCode(keyHashCache, second.Key),
                GetValueHashCode(second.Value));
        }

        HashCode hashCode = default;

        for (var i = 0; i < keyValuePairs.Length; i++)
        {
            ref readonly var item = ref keyValuePairs[i];
            hashCode.Add(GetKeyHashCode(keyHashCache, item.Key));
            hashCode.Add(GetValueHashCode(item.Value));
        }

        return hashCode.ToHashCode();
#else
        // Combine the inputs with a multiply-xor chain and a final avalanche step
        // (murmur3 fmix32) so that the low bits used for bucketing depend on every input.
        var hash = (uint)keyValuePairs.Length;

        for (var i = 0; i < keyValuePairs.Length; i++)
        {
            ref readonly var item = ref keyValuePairs[i];
            unchecked
            {
                hash = (hash ^ (uint)GetKeyHashCode(keyHashCache, item.Key)) * 0x9E3779B1u;
                hash = (hash ^ (uint)GetValueHashCode(item.Value)) * 0x9E3779B1u;
            }
        }

        unchecked
        {
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
        }

        return (int)hash;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetKeyHashCode(ThreadStaticStorage.KeyHashCacheEntry[] cache, string key)
    {
        var length = key.Length;
        var index = length == 0
            ? 0
            : (length + (key[0] << 1) + key[length - 1]) & (ThreadStaticStorage.KeyHashCacheSize - 1);

        ref var entry = ref cache[index];
        if (ReferenceEquals(entry.Key, key))
        {
            return entry.Hash;
        }

#if NET || NETSTANDARD2_1_OR_GREATER
        var hash = key.GetHashCode(StringComparison.Ordinal);
#else
        var hash = GetSeededStringHashCode(key);
#endif

        entry.Key = key;
        entry.Hash = hash;

        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetValueHashCode(object? value)
        => value switch
        {
            null => 0,
#if NET || NETSTANDARD2_1_OR_GREATER
            string s => s.GetHashCode(StringComparison.Ordinal),
#else
            string s => GetSeededStringHashCode(s),
#endif
            int i => GetSeededScalarHashCode(unchecked((uint)i), Int32HashSeed),
            bool b => b ? TrueHashCode : FalseHashCode,
            _ => GetNonStringValueHashCode(value),
        };

    private static int GetNonStringValueHashCode(object value)
    {
        // Mixing a value's GetHashCode cannot separate distinct values that
        // already share that hash. Hash the equality-relevant bits of wide
        // built-in values before folding them to 32 bits instead.
        unchecked
        {
            return value switch
            {
                uint ui => GetSeededScalarHashCode(ui, UInt32HashSeed),
#if NET || NETSTANDARD2_1_OR_GREATER
                float f => GetSeededScalarHashCode((uint)f.GetHashCode(), SingleHashSeed),
#else
                // .NET Framework gives equal NaN values different hashes.
                float f => GetSeededScalarHashCode(
                    (uint)(float.IsNaN(f) ? float.NaN : f).GetHashCode(),
                    SingleHashSeed),
#endif
                long l => GetSeededHashCode<long>((ulong)l),
                ulong ul => GetSeededHashCode<ulong>(ul),
                double d => GetSeededHashCode(d),
                decimal m => GetSeededHashCode(m),
                Guid guid => GetSeededHashCode(guid),
                DateTime dateTime => GetSeededHashCode<DateTime>((ulong)dateTime.Ticks),
                DateTimeOffset dateTimeOffset => GetSeededHashCode<DateTimeOffset>((ulong)dateTimeOffset.UtcTicks),
                TimeSpan timeSpan => GetSeededHashCode<TimeSpan>((ulong)timeSpan.Ticks),
                Enum e => GetSeededHashCode(e),
                byte b => GetSeededScalarHashCode<byte>(b),
                sbyte sb => GetSeededScalarHashCode<sbyte>((uint)sb),
                short s => GetSeededScalarHashCode<short>((uint)s),
                ushort us => GetSeededScalarHashCode<ushort>(us),
                char c => GetSeededScalarHashCode<char>(c),
                nint n => GetSeededHashCode<nint>((ulong)n),
                nuint un => GetSeededHashCode<nuint>((ulong)un),
#if NET
                Half h => GetSeededScalarHashCode<Half>((uint)h.GetHashCode()),
                DateOnly date => GetSeededScalarHashCode<DateOnly>((uint)date.DayNumber),
                TimeOnly time => GetSeededHashCode<TimeOnly>((ulong)time.Ticks),
                Int128 i128 => GetSeededHashCode<Int128>((ulong)i128, (ulong)(i128 >> 64)),
                UInt128 u128 => GetSeededHashCode<UInt128>((ulong)u128, (ulong)(u128 >> 64)),
#endif

                // Custom values may compare equal across runtime types.
                _ => GetSeededScalarHashCode((uint)value.GetHashCode(), CustomHashSeed),
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSeededScalarHashCode<T>(uint value)
        => GetSeededScalarHashCode(value, ValueHashSeed<T>.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSeededScalarHashCode(uint value, ulong seed)
    {
        // Use one mixing round for scalar hashes and two for wide inputs.
        unchecked
        {
            var hash = SplitMix64(seed + value);
            return (int)hash ^ (int)(hash >> 32);
        }
    }

    private static int GetSeededHashCode(double value)
    {
        // Equals treats both zeros as equal and all NaN representations as equal.
        if (value == 0)
        {
            value = 0;
        }
        else if (double.IsNaN(value))
        {
            value = double.NaN;
        }

        return GetSeededHashCode<double>(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    }

    private static int GetSeededHashCode(decimal value)
    {
#if NET
        Span<int> bits = stackalloc int[4];
        _ = decimal.GetBits(value, bits);
#else
        var bits = decimal.GetBits(value);
#endif

        unchecked
        {
            var low = (uint)bits[0];
            var mid = (uint)bits[1];
            var high = (uint)bits[2];
            var negative = bits[3] < 0;
            var scale = (uint)(bits[3] >> 16) & 0xFF;

            // Equals ignores the sign and scale of zero, and trailing zeros.
            if ((low | mid | high) == 0)
            {
                return GetSeededHashCode<decimal>(0UL);
            }

            while (scale > 0 && (low & 1) == 0)
            {
                // Divide the 96-bit significand by 10, retaining it only if exact.
                var remainder = ((ulong)(high % 10) << 32) | mid;
                var nextMid = (uint)(remainder / 10);

                remainder = ((remainder % 10) << 32) | low;
                if (remainder % 10 != 0)
                {
                    break;
                }

                low = (uint)(remainder / 10);
                mid = nextMid;
                high /= 10;
                scale--;
            }

            return GetSeededHashCode<decimal>(
                ((ulong)mid << 32) | low,
                ((ulong)((scale << 1) | (negative ? 1u : 0u)) << 32) | high);
        }
    }

    private static int GetSeededHashCode(Guid value)
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        Span<byte> bytes = stackalloc byte[16];
        _ = value.TryWriteBytes(bytes);

        return GetSeededHashCode<Guid>(BitConverter.ToUInt64(bytes), BitConverter.ToUInt64(bytes.Slice(8)));
#else
        var storage = new GuidHashCodeStorage { Value = value };

        return GetSeededHashCode<Guid>(storage.Low, storage.High);
#endif
    }

    private static int GetSeededHashCode(Enum value)
    {
        var seed = GetTypeHashSeed(value.GetType());
        return value.GetTypeCode() switch
        {
            TypeCode.Int64 => GetSeededHashCode(seed, unchecked((ulong)((IConvertible)value).ToInt64(CultureInfo.InvariantCulture))),
            TypeCode.UInt64 => GetSeededHashCode(seed, ((IConvertible)value).ToUInt64(CultureInfo.InvariantCulture)),
            _ => GetSeededScalarHashCode(unchecked((uint)value.GetHashCode()), seed),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSeededHashCode<T>(ulong low, ulong high = 0)
        => GetSeededHashCode(ValueHashSeed<T>.Value, low, high);

    private static int GetSeededHashCode(ulong seed, ulong low, ulong high = 0)
    {
        // Mix before reducing the words; adding their 32-bit halves directly to
        // System.HashCode still allows one word's contribution to cancel another.
        unchecked
        {
            var hash = SplitMix64(SplitMix64(seed + low) + high);
            return (int)hash ^ (int)(hash >> 32);
        }
    }

#if !NET && !NETSTANDARD2_1_OR_GREATER
    private static int GetSeededStringHashCode(string value)
    {
        // Produces a hash of a string tag key/value for the metric-point lookup dictionary on target
        // frameworks that lack System.HashCode. On .NET Framework, string.GetHashCode() is not
        // randomized by default and its 64-bit implementation stops at the first NUL character, so a
        // malicious actor who controls a tag value could precompute distinct values that share a hash
        // code and collapse every colliding tag set into one dictionary bucket - turning each lookup into
        // an O(n) scan of full string comparisons. Hashing the UTF-16 code units with a per-process random
        // seed and non-linear (splitmix64) mixing makes such collisions impossible to precompute and
        // independent of any embedded NUL. The seed is constant within a process, so equal tag sets
        // still hash equally and dictionary lookups remain correct.
        unchecked
        {
            var hash = HashSeed;

            foreach (var c in value)
            {
                hash = SplitMix64(hash + c);
            }

            hash = SplitMix64(hash + (uint)value.Length);

            return (int)hash ^ (int)(hash >> 32);
        }
    }

#endif

#if !NET && !NETSTANDARD2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private static ulong SplitMix64(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    private static ulong GenerateHashSeed()
    {
        var seed = new byte[sizeof(ulong)];

        using (var randomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            randomNumberGenerator.GetBytes(seed);
        }

        return BitConverter.ToUInt64(seed, 0);
    }

    // Mix the runtime type's full identity into the seed before hashing the value.
    private static ulong GetTypeHashSeed(Type type)
        => SplitMix64(HashSeed ^ unchecked((ulong)type.TypeHandle.Value.ToInt64()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SequenceEqual(
        ReadOnlySpan<KeyValuePair<string, object?>> ourKvps,
        ReadOnlySpan<KeyValuePair<string, object?>> theirKvps)
    {
        var length = ourKvps.Length;

        if (length != theirKvps.Length)
        {
            return false;
        }

        switch (length)
        {
            case 0:
                return true;
            case 1:
                return AreEqual(in ourKvps[0], in theirKvps[0]);
            case 2:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1]);
            case 3:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1])
                    && AreEqual(in ourKvps[2], in theirKvps[2]);
            case 4:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1])
                    && AreEqual(in ourKvps[2], in theirKvps[2])
                    && AreEqual(in ourKvps[3], in theirKvps[3]);
            case 5:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1])
                    && AreEqual(in ourKvps[2], in theirKvps[2])
                    && AreEqual(in ourKvps[3], in theirKvps[3])
                    && AreEqual(in ourKvps[4], in theirKvps[4]);
            case 6:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1])
                    && AreEqual(in ourKvps[2], in theirKvps[2])
                    && AreEqual(in ourKvps[3], in theirKvps[3])
                    && AreEqual(in ourKvps[4], in theirKvps[4])
                    && AreEqual(in ourKvps[5], in theirKvps[5]);
            case 7:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1])
                    && AreEqual(in ourKvps[2], in theirKvps[2])
                    && AreEqual(in ourKvps[3], in theirKvps[3])
                    && AreEqual(in ourKvps[4], in theirKvps[4])
                    && AreEqual(in ourKvps[5], in theirKvps[5])
                    && AreEqual(in ourKvps[6], in theirKvps[6]);
            case 8:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1])
                    && AreEqual(in ourKvps[2], in theirKvps[2])
                    && AreEqual(in ourKvps[3], in theirKvps[3])
                    && AreEqual(in ourKvps[4], in theirKvps[4])
                    && AreEqual(in ourKvps[5], in theirKvps[5])
                    && AreEqual(in ourKvps[6], in theirKvps[6])
                    && AreEqual(in ourKvps[7], in theirKvps[7]);
            case 9:
                return AreEqual(in ourKvps[0], in theirKvps[0])
                    && AreEqual(in ourKvps[1], in theirKvps[1])
                    && AreEqual(in ourKvps[2], in theirKvps[2])
                    && AreEqual(in ourKvps[3], in theirKvps[3])
                    && AreEqual(in ourKvps[4], in theirKvps[4])
                    && AreEqual(in ourKvps[5], in theirKvps[5])
                    && AreEqual(in ourKvps[6], in theirKvps[6])
                    && AreEqual(in ourKvps[7], in theirKvps[7])
                    && AreEqual(in ourKvps[8], in theirKvps[8]);
            default:
                for (var i = 0; i < length; i++)
                {
                    if (!AreEqual(in ourKvps[i], in theirKvps[i]))
                    {
                        return false;
                    }
                }

                return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreEqual(in KeyValuePair<string, object?> ours, in KeyValuePair<string, object?> theirs)
    {
        if (!string.Equals(ours.Key, theirs.Key, StringComparison.Ordinal))
        {
            return false;
        }

        var ourValue = ours.Value;
        var theirValue = theirs.Value;

        return ReferenceEquals(ourValue, theirValue)
            || (ourValue?.Equals(theirValue) ?? (theirValue == null));
    }

#if !NET && !NETSTANDARD2_1_OR_GREATER
    // Guid is a 128-bit value with no managed references. As with
    // MetricPointValueStorage, overlapping fields avoids a temporary array
    // on frameworks without Guid.TryWriteBytes.
    [StructLayout(LayoutKind.Explicit)]
    private struct GuidHashCodeStorage
    {
        [FieldOffset(0)]
        public Guid Value;

        [FieldOffset(0)]
        public ulong Low;

        [FieldOffset(8)]
        public ulong High;
    }
#endif

    private static class ValueHashSeed<T>
    {
        public static readonly ulong Value = GetTypeHashSeed(typeof(T));
    }
}
