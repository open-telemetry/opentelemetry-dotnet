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

    /// <summary>
    /// Determines whether <paramref name="pattern"/> is a simple <c>prefix*</c> pattern:
    /// exactly one <c>*</c>, located at the end, and no <c>?</c> at all.
    /// </summary>
    /// <param name="pattern">The pattern to inspect.</param>
    /// <param name="prefix">The literal prefix, when <paramref name="pattern"/> is a simple trailing-wildcard pattern.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="pattern"/> is a simple trailing-wildcard pattern.
    /// </returns>
    public static bool TryGetWildcardPrefix(string pattern, [NotNullWhen(true)] out string? prefix)
    {
        var lastIndex = pattern.Length - 1;

        if (lastIndex >= 0 && pattern[lastIndex] == '*')
        {
            for (var i = 0; i < lastIndex; i++)
            {
                if (pattern[i] is '*' or '?')
                {
                    prefix = null;
                    return false;
                }
            }

            prefix = pattern.Substring(0, lastIndex);
            return true;
        }

        prefix = null;
        return false;
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

        // RegexOptions.NonBacktracking is not used as it has a fixed automata-size limit that many
        // source patterns can exceed and it retains a much larger automaton per Regex instance
        // than a backtracking regex, which can lead to an OutOfMemoryException in applications.
        // The match timeout bounds worst-case matching time to protect against catastrophic backtracking.
        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7787.
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
