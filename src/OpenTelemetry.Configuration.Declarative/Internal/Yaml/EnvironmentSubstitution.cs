// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// DeclarativeConfigurationException carries the OTEL1006 experimental attribute.
// Suppress once here rather than at every throw site.
#pragma warning disable OTEL1006

using System.Text.RegularExpressions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Handles environment variable substitution in YAML scalar values, as defined by the OTel declarative config spec.
/// </summary>
/// <remarks>
/// Supported syntax:
/// <list type="bullet">
///   <item><c>${VAR}</c> or <c>${env:VAR}</c> - replaced with the value of VAR.</item>
///   <item><c>${VAR:-default}</c> - uses <c>default</c> when VAR is unset or empty.</item>
///   <item><c>$$</c> - escape sequence that produces a literal <c>$</c>.</item>
/// </list>
/// Unset variables with no default resolve to an empty string.
/// Malformed <c>${...}</c> expressions throw <see cref="DeclarativeConfigurationException"/>.
/// Substitution runs on already-parsed YAML scalar strings, so environment variables cannot inject YAML structure.
/// </remarks>
internal static partial class EnvironmentSubstitution
{
    // OTel substitution grammar: https://opentelemetry.io/docs/specs/otel/configuration/data-model/
    // Alternation order: $$ | ${env:NAME:-default} | invalid ${...} | unterminated ${
    // Groups: 1=env prefix, 2=name, 3=raw default (VCHAR-WSP-NO-RBRACE; $$ unescaped in evaluator).
    private const string SubstitutionPatternString =
        @"\$\$|\$\{(?:(env):)?([a-zA-Z_][a-zA-Z0-9_]*)(?::-([" + "\x09\x20" + @"-\x7C\x7E]*))?\}|\$\{[^}]*\}|\$\{";

#if !NET
    private static readonly Regex SubstitutionPatternInstance = new(
        SubstitutionPatternString,
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromSeconds(1));
#endif

    /// <summary>
    /// Returns a copy of <paramref name="value"/> with all substitution expressions replaced,
    /// using <paramref name="resolveVariable"/> to look up environment variable values.
    /// </summary>
    /// <param name="value">The scalar string to process.</param>
    /// <param name="resolveVariable">Returns the value of a named environment variable, or <see langword="null"/> if not set.</param>
    /// <returns>The string with all substitution expressions replaced.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <paramref name="value"/> contains a syntactically invalid <c>${...}</c> expression.
    /// </exception>
    internal static string Substitute(string value, Func<string, string?> resolveVariable)
    {
        Guard.ThrowIfNull(resolveVariable);

        if (value.Length == 0)
        {
            return value;
        }

        // Fast-path: skip the regex if there's no '$' in the string.
#if NET
        if (value.IndexOf('$', StringComparison.Ordinal) < 0)
#else
        if (value.IndexOf("$", StringComparison.Ordinal) < 0)
#endif
        {
            return value;
        }

        return GetSubstitutionPattern().Replace(value, match =>
        {
            // Case 1: $$ escape to literal $.
            if (match.Value == "$$")
            {
                return "$";
            }

            // Cases 3 & 4: group 2 (variable name) did not participate - either an invalid
            // ${...} reference (alt 3) or an unterminated ${ with no closing brace (alt 4).
            if (!match.Groups[2].Success)
            {
#if NET
                if (!match.Value.EndsWith('}'))
#else
                if (!match.Value.EndsWith("}", StringComparison.Ordinal))
#endif
                {
                    // Alt 4: ${ matched with no closing '}' anywhere after it.
                    throw new DeclarativeConfigurationException(
                        $"Value contains an unterminated environment variable substitution expression: " +
                        $"'${{' at position {match.Index} has no matching '}}'.");
                }

                // Alt 3: ${...} closed but alt 2 failed. Distinguish two cases so the error
                // message points at the actual problem rather than always blaming the name.
                var content = match.Value;
                var colonDash = content.IndexOf(":-", 2, StringComparison.Ordinal);
                if (colonDash > 0)
                {
                    // A ':-' separator is present. Check whether the part before it is a valid name;
                    // if so, the problem is the default-value content, not the name.
                    var nameStart = content.StartsWith("${env:", StringComparison.Ordinal) ? 6 : 2;
                    if (nameStart < colonDash && HasValidEnvName(content, nameStart, colonDash - nameStart))
                    {
                        throw new DeclarativeConfigurationException(
                            $"Value contains an environment variable substitution expression '{content}' with " +
                            "an invalid default value. Default values may only contain tab (U+0009), " +
                            "printable ASCII (U+0020-U+007C), and '~' (U+007E); other characters are not allowed.");
                    }
                }

                throw new DeclarativeConfigurationException(
                    $"Value contains an invalid environment variable substitution reference '{content}'. " +
                    "Valid syntax is ${ENV_NAME} or ${ENV_NAME:-default} where ENV_NAME starts with a " +
                    "letter or underscore and contains only letters, digits, and underscores.");
            }

            // Case 2: valid substitution.
            var name = match.Groups[2].Value;
            var hasDefault = match.Groups[3].Success;

            // Per spec, $$ unescapes to $ everywhere in the input, including inside default values.
            // The regex captures the raw default; unescape it here before use.
#pragma warning disable CA1307 // Specify StringComparison for clarity - Adds no real value and doesn't work for all TFMs
            var defaultValue = hasDefault ? match.Groups[3].Value.Replace("$$", "$") : string.Empty;
#pragma warning restore CA1307 // Specify StringComparison for clarity

            var envValue = resolveVariable(name);

            if (!hasDefault)
            {
                if (envValue is null)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.EnvironmentVariableNotSet(name);
                }
                else if (envValue.Length == 0)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.EnvironmentVariableEmpty(name);
                }
            }

            return envValue is null || envValue.Length == 0 ? defaultValue : envValue;
        });
    }

    /// <summary>
    /// Returns a copy of <paramref name="value"/> with all substitution expressions replaced,
    /// resolving against the current process environment variables.
    /// </summary>
    /// <param name="value">The scalar string value to process.</param>
    /// <returns>The string with substitution expressions replaced.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <paramref name="value"/> contains a syntactically invalid <c>${...}</c> reference.
    /// </exception>
    internal static string Substitute(string value)
        => Substitute(value, name => Environment.GetEnvironmentVariable(name));

    // Returns true when the substring of 'value' at [start, start+length) is a valid OTel env-var name:
    // starts with a letter or underscore, followed by letters, digits, or underscores.
    private static bool HasValidEnvName(string value, int start, int length)
    {
        var first = value[start];
        if (!((first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z') || first == '_'))
        {
            return false;
        }

        for (var i = start + 1; i < start + length; i++)
        {
            var c = value[i];
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

#if NET8_0_OR_GREATER
    [GeneratedRegex(SubstitutionPatternString, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GetSubstitutionPattern();
#else
    private static Regex GetSubstitutionPattern() => SubstitutionPatternInstance;
#endif
}
