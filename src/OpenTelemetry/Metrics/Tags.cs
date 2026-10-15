// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

internal readonly struct Tags : IEquatable<Tags>
{
    public static readonly Tags EmptyTags = new([]);

#if !NET && !NETSTANDARD2_1_OR_GREATER
    // A per-process random seed used to hash string tag keys/values
    // on target frameworks whose string.GetHashCode() is not randomized.
    private static readonly ulong HashSeed = GenerateHashSeed();
#endif

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
        HashCode hashCode = default;

        for (var i = 0; i < keyValuePairs.Length; i++)
        {
            ref readonly var item = ref keyValuePairs[i];
            hashCode.Add(GetKeyHashCode(keyHashCache, item.Key));
            hashCode.Add(item.Value);
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

#if !NET && !NETSTANDARD2_1_OR_GREATER
    private static int GetValueHashCode(object? value)
        => value is string s ? GetSeededStringHashCode(s) : (value?.GetHashCode() ?? 0);

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
#endif

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
}
