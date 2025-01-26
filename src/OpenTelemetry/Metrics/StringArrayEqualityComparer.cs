// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

internal sealed class StringArrayEqualityComparer : IEqualityComparer<string[]>
{
    public bool Equals(string[]? strings1, string[]? strings2)
    {
        if (ReferenceEquals(strings1, strings2))
        {
            return true;
        }

        if (ReferenceEquals(strings1, null) || ReferenceEquals(strings2, null))
        {
            return false;
        }

        var len1 = strings1.Length;

        if (len1 != strings2.Length)
        {
            return false;
        }

        for (int i = 0; i < len1; i++)
        {
            if (!strings1[i].Equals(strings2[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(string[] strings)
    {
        Debug.Assert(strings != null, "strings was null");

#if NET
        HashCode hashCode = default;

        foreach (var ch in strings)
        {
            hashCode.Add(ch);
        }

        var hash = hashCode.ToHashCode();
#else
        var hash = 17;

        for (int i = 0; i < strings!.Length; i++)
        {
            unchecked
            {
                hash = (hash * 31) + (strings[i]?.GetHashCode() ?? 0);
            }
        }
#endif

        return hash;
    }
}
