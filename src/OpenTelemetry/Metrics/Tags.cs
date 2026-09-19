// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

internal readonly struct Tags : IEquatable<Tags>
{
    public static readonly Tags EmptyTags = new([]);

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
        // the entries (almost always) share the same tag keys. Hashing each key
        // string costs a full string hash but adds little entropy to the bucket
        // distribution, so only a constant-time fingerprint of the key (its length
        // and first two and last two characters) is mixed in; the values, which carry the
        // entropy, are hashed in full. Tag sets whose keys share a fingerprint and
        // whose values are identical collide on the hash and are then told apart by
        // Equals, which compares the keys first.
#if NET || NETSTANDARD2_1_OR_GREATER
        HashCode hashCode = default;

        for (var i = 0; i < keyValuePairs.Length; i++)
        {
            ref readonly var item = ref keyValuePairs[i];
            hashCode.Add(GetKeyFingerprint(item.Key));
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
                hash = (hash ^ (uint)GetKeyFingerprint(item.Key)) * 0x9E3779B1u;
                hash = (hash ^ (uint)(item.Value?.GetHashCode() ?? 0)) * 0x9E3779B1u;
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
    private static int GetKeyFingerprint(string key)
    {
        // Constant-time stand-in for the key's hash: its length combined with its
        // first two and last two characters. Cheap next to the value hash, while
        // still separating dynamically generated keys of equal length that vary
        // in a prefix or suffix (for example "flag_017" or "017_flag").
        var length = key.Length;

        if (length < 2)
        {
            return length == 0 ? 0 : key[0];
        }

        return length
            ^ (key[0] << 8)
            ^ (key[1] << 16)
            ^ (key[length - 2] << 4)
            ^ (key[length - 1] << 12);
    }

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
