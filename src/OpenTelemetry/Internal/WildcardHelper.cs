// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OpenTelemetry;

internal static class WildcardHelper
{
    public static bool ContainsWildcard(
        [NotNullWhen(true)]
        string? value)
    {
        if (value == null)
        {
            return false;
        }

#if NET || NETSTANDARD2_1_OR_GREATER
        return value.Contains('*', StringComparison.Ordinal) || value.Contains('?', StringComparison.Ordinal);
#else
        return value.Contains('*') || value.Contains('?');
#endif
    }

    public static bool MatchAny(IEnumerable<string> templates, string s)
    {
        foreach (string wildcard in templates)
        {
            if (wildcard.WildcardMatch(s, 0, 0, true))
            {
                return true;
            }
        }

        return false;
    }

    // Taken from https://github.com/picrap/WildcardMatch/blob/master/WildcardMatch/StringExtensions.cs
    public static bool WildcardMatch(this string wildcard, string s, int wildcardIndex, int sIndex, bool ignoreCase)
    {
        for (; ;)
        {
            // in the wildcard end, if we are at tested string end, then strings match
            if (wildcardIndex == wildcard.Length)
            {
                return sIndex == s.Length;
            }

            var c = wildcard[wildcardIndex];
            switch (c)
            {
                // always a match
                case '?':
                    break;
                case '*':
                    // if this is the last wildcard char, then we have a match, whatever the tested string is
                    if (wildcardIndex == wildcard.Length - 1)
                    {
                        return true;
                    }

                    // test if a match follows
                    return Enumerable.Range(sIndex, s.Length - sIndex).Any(i => WildcardMatch(wildcard, s, wildcardIndex + 1, i, ignoreCase));
                default:
                    var cc = ignoreCase ? char.ToLower(c, CultureInfo.InvariantCulture) : c;
                    if (s.Length == sIndex)
                    {
                        return false;
                    }

                    var sc = ignoreCase ? char.ToLower(s[sIndex], CultureInfo.InvariantCulture) : s[sIndex];
                    if (cc != sc)
                    {
                        return false;
                    }

                    break;
            }

            wildcardIndex++;
            sIndex++;
        }
    }
}
