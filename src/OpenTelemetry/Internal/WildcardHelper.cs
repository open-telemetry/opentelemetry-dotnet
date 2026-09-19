// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OpenTelemetry;

internal static class WildcardHelper
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

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
        try
        {
            return new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.IgnoreCase);
        }
        catch (NotSupportedException)
        {
            // RegexOptions.NonBacktracking has a fixed limit on the size of the automata it can
            // build (e.g., 1,000 nodes on .NET 8, larger on later versions). A very large number
            // of source/meter patterns can exceed that limit and cause the constructor to throw.
            // If this happens, fall back to a backtracking regex bounded by a match timeout.
            // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7787.
        }
#endif

        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexMatchTimeout);
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
