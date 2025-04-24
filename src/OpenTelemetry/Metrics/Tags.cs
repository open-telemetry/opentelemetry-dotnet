// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET
using System.Runtime.InteropServices;
#endif

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
    {
        return obj is Tags other && this.Equals(other);
    }

    public readonly bool Equals(Tags other)
    {
        var ourKvps = this.KeyValuePairs;
        var theirKvps = other.KeyValuePairs;

        var length = ourKvps.Length;

        if (length != theirKvps.Length)
        {
            return false;
        }

#if NET
        // Note: This loop uses unsafe code (pointers) to elide bounds checks on
        // two arrays we know to be of equal length.
        if (length > 0)
        {
            ref var ours = ref MemoryMarshal.GetArrayDataReference(ourKvps);
            ref var theirs = ref MemoryMarshal.GetArrayDataReference(theirKvps);
            while (true)
            {
                // Note: string.Equals performs an ordinal comparison
                if (!ours.Key.Equals(theirs.Key, StringComparison.Ordinal))
                {
                    return false;
                }

                if (!ours.Value?.Equals(theirs.Value) ?? theirs.Value != null)
                {
                    return false;
                }

                if (--length == 0)
                {
                    break;
                }

                ours = ref Unsafe.Add(ref ours, 1);
                theirs = ref Unsafe.Add(ref theirs, 1);
            }
        }
#else
        for (int i = 0; i < length; i++)
        {
            ref var ours = ref ourKvps[i];

            // Note: Bounds check happens here for theirKvps element access
            ref var theirs = ref theirKvps[i];

            // Note: string.Equals performs an ordinal comparison
            if (!ours.Key.Equals(theirs.Key, StringComparison.Ordinal))
            {
                return false;
            }

            if (!ours.Value?.Equals(theirs.Value) ?? theirs.Value != null)
            {
                return false;
            }
        }
#endif

        return true;
    }

    public override readonly int GetHashCode() => this.hashCode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeHashCode(KeyValuePair<string, object?>[] keyValuePairs)
    {
        Debug.Assert(keyValuePairs != null, "keyValuePairs was null");

#if NET
        HashCode hashCode = default;

        for (int i = 0; i < keyValuePairs.Length; i++)
        {
            ref var item = ref keyValuePairs[i];
            hashCode.Add(item.Key.GetHashCode());
            hashCode.Add(item.Value);
        }

        return hashCode.ToHashCode();
#else
        var hash = 17;

        for (int i = 0; i < keyValuePairs!.Length; i++)
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
}
