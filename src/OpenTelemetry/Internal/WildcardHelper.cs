// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
        if (!value.Contains('*', StringComparison.Ordinal) && !value.Contains('?', StringComparison.Ordinal))
        {
            return false;
        }

        if (!value.Contains('\\', StringComparison.Ordinal))
        {
            return true;
        }
#else
        if (!value.Contains('*') && !value.Contains('?'))
        {
            return false;
        }

        if (!value.Contains('\\'))
        {
            return true;
        }
#endif

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '*' or '?')
            {
                var backslashCount = 0;
                for (var j = i - 1; j >= 0 && value[j] == '\\'; j--)
                {
                    backslashCount++;
                }

                if ((backslashCount & 1) == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether <paramref name="pattern"/> is a simple <c>prefix*</c> pattern:
    /// exactly one unescaped <c>*</c>, located at the end, and no unescaped <c>?</c> at all.
    /// </summary>
    /// <param name="pattern">The pattern to inspect.</param>
    /// <param name="prefix">The unescaped literal prefix, when <paramref name="pattern"/> is a simple trailing-wildcard pattern.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="pattern"/> is a simple trailing-wildcard pattern.
    /// </returns>
    public static bool TryGetWildcardPrefix(string pattern, [NotNullWhen(true)] out string? prefix)
    {
        var lastIndex = pattern.Length - 1;

        if (lastIndex >= 0 && pattern[lastIndex] == '*')
        {
            var trailingBackslashCount = 0;
            for (var j = lastIndex - 1; j >= 0 && pattern[j] == '\\'; j--)
            {
                trailingBackslashCount++;
            }

            // If the trailing '*' is escaped (odd number of preceding backslashes), it is not a wildcard.
            if ((trailingBackslashCount & 1) != 0)
            {
                prefix = null;
                return false;
            }

            for (var i = 0; i < lastIndex; i++)
            {
                if (pattern[i] is '*' or '?')
                {
                    var backslashCount = 0;
                    for (var j = i - 1; j >= 0 && pattern[j] == '\\'; j--)
                    {
                        backslashCount++;
                    }

                    if ((backslashCount & 1) == 0)
                    {
                        prefix = null;
                        return false;
                    }
                }
            }

            prefix = Unescape(pattern.Substring(0, lastIndex));
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
            from p in patterns select "(?:" + PatternToRegex(p) + ')');

        var pattern = "^(?:" + convertedPattern + ")$";

        // RegexOptions.NonBacktracking is not used as it has a fixed automata-size limit that many
        // source patterns can exceed and it retains a much larger automaton per Regex instance
        // than a backtracking regex, which can lead to an OutOfMemoryException in applications.
        // The match timeout bounds worst-case matching time to protect against catastrophic backtracking.
        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7787.
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexMatchTimeout);
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

    /// <summary>
    /// Unescapes escaped wildcards (<c>\*</c> -> <c>*</c>, <c>\?</c> -> <c>?</c>) and escaped backslashes (<c>\\</c> -> <c>\</c>).
    /// </summary>
    /// <param name="pattern">The pattern to unescape.</param>
    /// <returns>The unescaped string.</returns>
    public static string Unescape(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return pattern;
        }

#if NET || NETSTANDARD2_1_OR_GREATER
        if (!pattern.Contains('\\', StringComparison.Ordinal))
        {
            return pattern;
        }
#else
        if (!pattern.Contains('\\'))
        {
            return pattern;
        }
#endif

        var sb = new StringBuilder(pattern.Length);
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                var next = pattern[i + 1];
                if (next is '*' or '?' or '\\')
                {
                    sb.Append(next);
                    i++;
                    continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string PatternToRegex(string pattern)
    {
        var sb = new StringBuilder(pattern.Length * 2);
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                var next = pattern[i + 1];
                if (next is '*' or '?' or '\\')
                {
                    sb.Append(Regex.Escape(next.ToString()));
                    i++;
                    continue;
                }
            }

            if (c == '*')
            {
                sb.Append(".*");
            }
            else if (c == '?')
            {
                sb.Append('.');
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
            }
        }

        return sb.ToString();
    }
}
