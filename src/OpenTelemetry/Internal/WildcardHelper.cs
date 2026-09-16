// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OpenTelemetry;

internal static class WildcardHelper
{
#if !NET
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);
#endif

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

    public static Regex GetWildcardRegex(IEnumerable<string> patterns)
    {
        Debug.Assert(patterns?.Any() == true, "patterns was null or empty");

        var convertedPattern = string.Join(
            "|",
#if NET || NETSTANDARD2_1_OR_GREATER
            from p in patterns select "(?:" + Regex.Escape(p).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + ')');
#else
            from p in patterns select "(?:" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + ')');
#endif

        var pattern = "^(?:" + convertedPattern + ")$";

#if NET
        return new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.IgnoreCase);
#else
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexMatchTimeout);
#endif
    }

    public static bool IsMatch(Regex regex, string input)
    {
        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
