// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Handles environment variable substitution in YAML scalar values, as defined by the OTel declarative config spec.
/// </summary>
/// <remarks>
/// <para>
/// Supported syntax:
/// <list type="bullet">
///   <item><c>${VAR}</c> or <c>${env:VAR}</c> - replaced with the value of VAR.</item>
///   <item><c>${VAR:-default}</c> - uses <c>default</c> when VAR is unset or empty.</item>
///   <item><c>$$</c> - escape sequence that produces a literal <c>$</c>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Escape sequences take precedence over substitution references.</b> Each <c>$$</c> splits the
/// input into independently substituted regions, and the <c>$</c> emitted for the escape is never
/// reconsidered. A reference whose text straddles a <c>$$</c> is therefore not a reference at all
/// and is emitted literally - <c>${VAR:-a$$b}</c> yields the literal <c>${VAR:-a$b}</c>. This is
/// what the spec's example table requires; see the <c>${UNDEFINED_KEY:-$${UNDEFINED_KEY}}</c> row.
/// </para>
/// <para>
/// A <c>${</c> with no closing <c>}</c> in its region cannot form a reference either, so it is also
/// emitted literally (with a verbose diagnostic event). A well-formed <c>${...}</c> whose content
/// violates the <c>env</c> scheme - <c>${VAR:?err}</c>, <c>${MY.VAR}</c>, <c>${}</c> - is an error
/// and throws <see cref="DeclarativeConfigurationException"/>, as the spec requires.
/// </para>
/// <para>
/// Substitution runs on already-parsed YAML scalar strings, so environment variables cannot inject
/// YAML structure, and resolved values are never rescanned for further references.
/// </para>
/// </remarks>
internal static class EnvironmentSubstitution
{
    /// <summary>
    /// Returns a copy of <paramref name="value"/> with all substitution expressions replaced,
    /// using <paramref name="resolveVariable"/> to look up environment variable values.
    /// </summary>
    /// <param name="value">The scalar string to process.</param>
    /// <param name="resolveVariable">Returns the value of a named environment variable, or <see langword="null"/> if not set.</param>
    /// <returns>The string with all substitution expressions replaced.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <paramref name="value"/> contains a well-formed <c>${...}</c> expression whose
    /// content is not a valid environment variable reference.
    /// </exception>
    internal static string Substitute(string value, Func<string, string?> resolveVariable)
    {
        Guard.ThrowIfNull(value);
        Guard.ThrowIfNull(resolveVariable);

        if (value.Length == 0)
        {
            return value;
        }

        // Fast-path: skip scanning if there's no '$' in the string.
#if NET
        if (value.IndexOf('$', StringComparison.Ordinal) < 0)
#else
        if (value.IndexOf("$", StringComparison.Ordinal) < 0)
#endif
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        var regionStart = 0;

        while (true)
        {
            var escapeIndex = value.IndexOf("$$", regionStart, StringComparison.Ordinal);
            var regionEnd = escapeIndex < 0 ? value.Length : escapeIndex;

            AppendSubstitutedRegion(value, regionStart, regionEnd, resolveVariable, sb);

            if (escapeIndex < 0)
            {
                break;
            }

            sb.Append('$');
            regionStart = escapeIndex + 2;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns a copy of <paramref name="value"/> with all substitution expressions replaced,
    /// resolving against the current process environment variables.
    /// </summary>
    /// <param name="value">The scalar string value to process.</param>
    /// <returns>The string with substitution expressions replaced.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <paramref name="value"/> contains a well-formed <c>${...}</c> expression whose
    /// content is not a valid environment variable reference.
    /// </exception>
    internal static string Substitute(string value)
        => Substitute(value, name => Environment.GetEnvironmentVariable(name));

    // Substitutes every complete reference in value[start, end). The region boundaries are escape
    // boundaries, so a '}' outside them cannot close a '${' inside them.
    private static void AppendSubstitutedRegion(
        string value,
        int start,
        int end,
        Func<string, string?> resolveVariable,
        StringBuilder output)
    {
        var i = start;
        while (i < end)
        {
            if (value[i] != '$' || i + 1 >= end || value[i + 1] != '{')
            {
                output.Append(value[i]);
                i++;
                continue;
            }

            var exprStart = i;
            var closingBrace = value.IndexOf('}', exprStart + 2, end - (exprStart + 2));
            if (closingBrace < 0)
            {
                // No '}' in the remainder of this region, so no complete SUBSTITUTION-REF exists
                // here (nor after: any later '}' would have been found by this search). The text
                // is not a reference and must be emitted as-is.
                var literal = value.Substring(exprStart, end - exprStart);
                OpenTelemetryDeclarativeConfigurationEventSource.Log.UnresolvedSubstitutionExpression(literal);
                output.Append(literal);
                return;
            }

            output.Append(ResolveSubstitution(value, exprStart, closingBrace, resolveVariable));
            i = closingBrace + 1;
        }
    }

    // Resolves the complete expression value[exprStart..closingBrace]. exprStart points at '$' and
    // closingBrace at the matching '}'.
    private static string ResolveSubstitution(
        string value,
        int exprStart,
        int closingBrace,
        Func<string, string?> resolveVariable)
    {
        var i = exprStart + 2;

        // Consume the optional 'env:' prefix. 'e', 'n', 'v' and ':' are never '}', so this can
        // never step past closingBrace.
        if (closingBrace - i >= 4 && value.AsSpan(i, 4).SequenceEqual("env:"))
        {
            i += 4;
        }

        // Parse the variable name: [a-zA-Z_][a-zA-Z0-9_]*. The scan stops at closingBrace because
        // '}' is not a name character.
        var nameStart = i;
        if (i < closingBrace && IsEnvNameStart(value[i]))
        {
            i++;
            while (i < closingBrace && IsEnvNameContinue(value[i]))
            {
                i++;
            }
        }

        if (i == nameStart)
        {
            throw CreateInvalidReferenceException(value, exprStart, closingBrace);
        }

        var name = value.Substring(nameStart, i - nameStart);
        bool hasDefault;
        var defaultStart = closingBrace;

        if (i == closingBrace)
        {
            hasDefault = false;
        }
        else if (value[i] == ':' && i + 1 < closingBrace && value[i + 1] == '-')
        {
            hasDefault = true;
            defaultStart = i + 2;
            ValidateDefaultValue(value, defaultStart, closingBrace, exprStart);
        }
        else
        {
            // The name parsed but is followed by something that is not '}' or ':-'. Either an
            // unrecognised PREFIX (${file:x}) or an invalid name (${MY.VAR}, ${VAR:?err}).
            throw CreateInvalidReferenceException(value, exprStart, closingBrace);
        }

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

        if (envValue is not null && envValue.Length > 0)
        {
            return envValue;
        }

        // Materialise the default only when it is actually used.
        return hasDefault ? value.Substring(defaultStart, closingBrace - defaultStart) : string.Empty;
    }

    // The default value is always validated, whether or not it ends up being used: malformed input
    // is an error regardless of the current environment.
    private static void ValidateDefaultValue(string value, int start, int end, int exprStart)
    {
        for (var i = start; i < end; i++)
        {
            if (!IsValidDefaultChar(value[i]))
            {
                // Name the offending code point explicitly. The expression is echoed as decoded
                // text, so an offending control character, DEL, or non-ASCII character is either
                // invisible or indistinguishable from a legal one in the message alone.
                throw new DeclarativeConfigurationException(
                    $"Value contains an environment variable substitution expression " +
                    $"'{value.Substring(exprStart, end + 1 - exprStart)}' at position {exprStart} with an invalid " +
                    $"default value: the character at offset {i - start} of the default value is " +
                    $"U+{(int)value[i]:X4}. Default values may only contain tab (U+0009), printable ASCII " +
                    "(U+0020-U+007C), and '~' (U+007E); other characters are not allowed. A YAML escape " +
                    "does not exempt a character: escapes are decoded before substitution runs.");
            }
        }
    }

    private static DeclarativeConfigurationException CreateInvalidReferenceException(string value, int exprStart, int closingBrace) =>
        new(
            $"Value contains an invalid environment variable substitution reference " +
            $"'{value.Substring(exprStart, closingBrace + 1 - exprStart)}' at position {exprStart}. " +
            "Valid syntax is ${ENV_NAME} or ${ENV_NAME:-default} where ENV_NAME starts with a " +
            "letter or underscore and contains only letters, digits, and underscores.");

    private static bool IsEnvNameStart(char c)
        => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';

    private static bool IsEnvNameContinue(char c)
        => char.IsAsciiLetterOrDigit(c) || c == '_';

    // OTel VCHAR-WSP-NO-RBRACE = %x21-7C / "~" / WSP. WSP contributes TAB (U+0009) and SPACE
    // (U+0020); the range covers U+0021-U+007C. '}' (U+007D) and DEL (U+007F) are excluded.
    private static bool IsValidDefaultChar(char c)
        => c == '\t' || (c >= '\x20' && c <= '\x7C') || c == '\x7E';
}
