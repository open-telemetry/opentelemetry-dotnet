// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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

        var length = ourKvps.Length;

        if (length != theirKvps.Length)
        {
            return false;
        }

        if (this.hashCode != other.hashCode)
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

    public override readonly int GetHashCode() => this.hashCode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeHashCode(KeyValuePair<string, object?>[] keyValuePairs)
    {
        Debug.Assert(keyValuePairs != null, "keyValuePairs was null");

#if NET || NETSTANDARD2_1_OR_GREATER
        HashCode hashCode = default;

        for (var i = 0; i < keyValuePairs.Length; i++)
        {
            ref var item = ref keyValuePairs[i];
            hashCode.Add(item.Key.GetHashCode(StringComparison.Ordinal));
            hashCode.Add(item.Value);
        }

        return hashCode.ToHashCode();
#else
        var hash = 17;

        for (var i = 0; i < keyValuePairs!.Length; i++)
        {
            ref var item = ref keyValuePairs[i];
            unchecked
            {
                hash = (hash * 31) + item.Key.GetHashCode();
                hash = (hash * 31) + (item.Value?.GetHashCode() ?? 0);
            }
        }

        return hash;
#endif
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
