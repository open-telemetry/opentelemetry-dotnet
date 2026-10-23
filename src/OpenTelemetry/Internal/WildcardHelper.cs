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
            from p in patterns select "(?:" + ConvertToRegexPattern(p) + ')');

        var pattern = "^(?:" + convertedPattern + ")$";

        // RegexOptions.NonBacktracking is not used as it has a fixed automata-size limit that many
        // source patterns can exceed and it retains a much larger automaton per Regex instance
        // than a backtracking regex, which can lead to an OutOfMemoryException in applications.
        // Atomic groups in ConvertToRegexPattern prevent backtracking between stars.
        // The match timeout remains as a safety net.
        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7787.
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexMatchTimeout);
    }

    /// <summary>
    /// Converts a wildcard pattern to an unanchored regular expression pattern
    /// without backtracking between <c>*</c> wildcards.
    /// </summary>
    /// <param name="pattern">The wildcard pattern to convert.</param>
    /// <param name="questionMarkIsWildcard">Whether <c>?</c> matches a single character or is literal.</param>
    /// <returns>The regular expression pattern.</returns>
    public static string ConvertToRegexPattern(string pattern, bool questionMarkIsWildcard = true)
    {
        var segments = pattern.Split('*');
        var lastIndex = segments.Length - 1;
        var builder = new StringBuilder();

        for (var i = 0; i <= lastIndex; i++)
        {
            var segment = Regex.Escape(segments[i]);

            if (questionMarkIsWildcard)
            {
#if NET || NETSTANDARD2_1_OR_GREATER
                segment = segment.Replace("\\?", ".", StringComparison.Ordinal);
#else
                segment = segment.Replace("\\?", ".");
#endif
            }

            if (i == 0)
            {
                builder.Append(segment);
            }
            else if (i == lastIndex)
            {
                builder.Append(".*").Append(segment);
            }
            else if (segment.Length > 0)
            {
                // Segments between stars have a fixed width. Commit to the first
                // match so a later failure cannot redistribute characters among
                // preceding stars. The final star can still find a matching suffix.
                builder.Append("(?>.*?").Append(segment).Append(')');
            }
        }

        return builder.ToString();
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
