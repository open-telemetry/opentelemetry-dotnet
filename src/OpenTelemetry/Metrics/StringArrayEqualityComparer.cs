// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

internal sealed class StringArrayEqualityComparer : IEqualityComparer<string[]>
{
    public bool Equals(string[]? x, string[]? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        var len1 = x.Length;

        if (len1 != y.Length)
        {
            return false;
        }

        for (var i = 0; i < len1; i++)
        {
            if (!string.Equals(x[i], y[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(string[] obj)
    {
        Debug.Assert(obj != null, "obj was null");

#if NET || NETSTANDARD2_1_OR_GREATER
        HashCode hashCode = default;

        for (var i = 0; i < obj.Length; i++)
        {
            hashCode.Add(obj[i]);
        }

        var hash = hashCode.ToHashCode();
#else
        var hash = 17;

        for (var i = 0; i < obj!.Length; i++)
        {
            unchecked
            {
                hash = (hash * 31) + (obj[i]?.GetHashCode() ?? 0);
            }
        }
#endif

        return hash;
    }
}
