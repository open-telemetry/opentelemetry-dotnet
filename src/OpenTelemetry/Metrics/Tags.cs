// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace OpenTelemetry.Metrics;

internal readonly struct Tags : IEquatable<Tags>
{
    public static readonly Tags EmptyTags = new(Array.Empty<KeyValuePair<string, object?>>());

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

        if (ourKvps.Length != theirKvps.Length)
        {
            return false;
        }

#if NET6_0_OR_GREATER
        // Note: This loop uses unsafe code (pointers) to elide bounds checks on
        // two arrays we know to be of equal length.
        var cursor = ourKvps.Length;
        if (cursor > 0)
        {
            ref var ours = ref MemoryMarshal.GetArrayDataReference(ourKvps);
            ref var theirs = ref MemoryMarshal.GetArrayDataReference(theirKvps);
            while (true)
            {
                // Equality check for Keys
                if (!StringComparer.OrdinalIgnoreCase.Equals(ours.Key, theirs.Key))
                {
                    return false;
                }

                // Equality check for Values
                if (!ours.Value?.Equals(theirs.Value) ?? theirs.Value != null)
                {
                    return false;
                }

                if (--cursor == 0)
                {
                    break;
                }

                ours = ref Unsafe.Add(ref ours, 1);
                theirs = ref Unsafe.Add(ref theirs, 1);
            }
        }
#else
        for (int i = 0; i < ourKvps.Length; i++)
        {
            ref var ours = ref ourKvps[i];

            // Note: Bounds check happens here for theirKvps element access
            ref var theirs = ref theirKvps[i];

            // Equality check for Keys
            if (!StringComparer.OrdinalIgnoreCase.Equals(ours.Key, theirs.Key))
            {
                return false;
            }

            // Equality check for Values
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

#if NET6_0_OR_GREATER
        HashCode hashCode = default;

        for (int i = 0; i < keyValuePairs.Length; i++)
        {
            ref var item = ref keyValuePairs[i];
            hashCode.Add(StringComparer.OrdinalIgnoreCase.GetHashCode(item.Key));
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
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(item.Key);
                hash = (hash * 31) + (item.Value?.GetHashCode() ?? 0);
            }
        }

        return hash;
#endif
    }
}
