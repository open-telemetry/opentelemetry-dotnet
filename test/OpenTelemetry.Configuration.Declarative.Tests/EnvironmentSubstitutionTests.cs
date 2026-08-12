// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class EnvironmentSubstitutionTests
{
    // The environment defined by the spec's example table:
    // https://opentelemetry.io/docs/specs/otel/configuration/data-model/#environment-variable-substitution
    private static readonly Dictionary<string, string> SpecEnvironment = new(StringComparer.Ordinal)
    {
        ["STRING_VALUE"] = "value",
        ["BOOL_VALUE"] = "true",
        ["INT_VALUE"] = "1",
        ["FLOAT_VALUE"] = "1.1",
        ["HEX_VALUE"] = "0xdeadbeef",
        ["INVALID_MAP_VALUE"] = "value\nkey:value",
        ["DO_NOT_REPLACE_ME"] = "Never use this value",
        ["REPLACE_ME"] = "${DO_NOT_REPLACE_ME}",
        ["VALUE_WITH_ESCAPE"] = "value$$",
    };

    // Every substitution row of the spec's example table, transcribed verbatim. This is the
    // authoritative oracle for this class: when the spec table changes, change this theory.
    [Theory]
    [InlineData("${STRING_VALUE}", "value")]
    [InlineData("${BOOL_VALUE}", "true")]
    [InlineData("${INT_VALUE}", "1")]
    [InlineData("${FLOAT_VALUE}", "1.1")]
    [InlineData("${HEX_VALUE}", "0xdeadbeef")]
    [InlineData("${env:STRING_VALUE}", "value")]
    [InlineData("${INVALID_MAP_VALUE}", "value\nkey:value")] // map structure is NOT expanded
    [InlineData("foo ${STRING_VALUE} ${FLOAT_VALUE}", "foo value 1.1")]
    [InlineData("${UNDEFINED_KEY}", "")]
    [InlineData("${UNDEFINED_KEY:-fallback}", "fallback")]
    [InlineData("${REPLACE_ME}", "${DO_NOT_REPLACE_ME}")] // not substituted recursively
    [InlineData("${UNDEFINED_KEY:-${STRING_VALUE}}", "${STRING_VALUE}")] // default not substituted recursively
    [InlineData("$${STRING_VALUE}", "${STRING_VALUE}")]
    [InlineData("$$${STRING_VALUE}", "$value")]
    [InlineData("$$$${STRING_VALUE}", "$${STRING_VALUE}")]
    [InlineData("$${STRING_VALUE:-fallback}", "${STRING_VALUE:-fallback}")]
    [InlineData("$${STRING_VALUE:-${STRING_VALUE}}", "${STRING_VALUE:-value}")]
    [InlineData("${UNDEFINED_KEY:-$${UNDEFINED_KEY}}", "${UNDEFINED_KEY:-${UNDEFINED_KEY}}")]
    [InlineData("${VALUE_WITH_ESCAPE}", "value$$")] // resolved values are NOT re-escaped or rescanned
    [InlineData("a $$ b", "a $ b")]
    [InlineData("a $ b", "a $ b")]
    public void Substitute_SpecExampleTable_MatchesExpectedOutput(string input, string expected) =>
        Assert.Equal(expected, EnvironmentSubstitution.Substitute(input, ResolveSpecVariable));

    // The one spec table row that is an error rather than an output.
    [Fact]
    public void Substitute_SpecInvalidReferenceRow_Throws() =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${STRING_VALUE:?error}", ResolveSpecVariable));

    // The DEFAULT-VALUE charset restriction applies to the *expression text*, never to the value an
    // environment variable resolves to. Guards the spec's INVALID_MAP_VALUE row against a future
    // "tidy-up" that validates resolved values too.
    [Fact]
    public void Substitute_ResolvedValueWithControlCharacters_IsPassedThroughUnchanged() =>
        Assert.Equal(
            "line1\nline2\ttab\x7f",
            EnvironmentSubstitution.Substitute("${VAR}", _ => "line1\nline2\ttab\x7f"));

    [Fact]
    public void Substitute_SimpleVar_ReturnsValue()
    {
        var result = EnvironmentSubstitution.Substitute("${MY_VAR}", name => name == "MY_VAR" ? "hello" : null);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Substitute_EnvPrefixedVar_ReturnsValue()
    {
        var result = EnvironmentSubstitution.Substitute("${env:MY_VAR}", name => name == "MY_VAR" ? "hello" : null);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Substitute_EnvPrefixedVarWithDefault_ReturnsValue()
    {
        var result = EnvironmentSubstitution.Substitute("${env:MY_VAR:-fallback}", _ => null);

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void Substitute_DefaultUsedWhenUndefined()
    {
        var result = EnvironmentSubstitution.Substitute("${UNDEFINED_VAR:-my-default}", _ => null);

        Assert.Equal("my-default", result);
    }

    // A set variable wins over its default. Complements Substitute_EmptyStringValue_UsesDefault.
    [Fact]
    public void Substitute_SetValueWithDefaultPresent_PrefersEnvironmentValue()
    {
        var result = EnvironmentSubstitution.Substitute("${MY_VAR:-fallback}", _ => "actual");

        Assert.Equal("actual", result);
    }

    [Fact]
    public void Substitute_EmptyDefault_ResolvesToEmpty()
    {
        var result = EnvironmentSubstitution.Substitute("prefix-${MY_VAR:-}-suffix", _ => null);

        Assert.Equal("prefix--suffix", result);
    }

    [Fact]
    public void Substitute_UndefinedNoDefault_BecomesEmpty()
    {
        var result = EnvironmentSubstitution.Substitute("prefix-${UNDEFINED_VAR}-suffix", _ => null);

        Assert.Equal("prefix--suffix", result);
    }

    [Fact]
    public void Substitute_EscapedDollar_ProducesLiteralBraceExpression()
    {
        // $$ collapses to $, so $${VAR} yields the literal string ${VAR}.
        var result = EnvironmentSubstitution.Substitute("$${VAR}", _ => "should-not-be-returned");

        Assert.Equal("${VAR}", result);
    }

    [Fact]
    public void Substitute_DoubleDollarNotFollowedByBrace_BecomesLiteralDollar()
    {
        var result = EnvironmentSubstitution.Substitute("$$plain", _ => null);

        Assert.Equal("$plain", result);
    }

    [Fact]
    public void Substitute_NoPlaceholders_ReturnsOriginalUnchanged()
    {
        var result = EnvironmentSubstitution.Substitute("no substitutions here", _ => null);

        Assert.Equal("no substitutions here", result);
    }

    [Fact]
    public void Substitute_MultipleVarsInOneString()
    {
        var result = EnvironmentSubstitution.Substitute(
            "${HOST}:${PORT:-8080}",
            name => name switch
            {
                "HOST" => "localhost",
                _ => null,
            });

        Assert.Equal("localhost:8080", result);
    }

    [Fact]
    public void Substitute_EmptyStringValue_UsesDefault()
    {
        // An empty resolved value is treated as "not set" and the default is used.
        var result = EnvironmentSubstitution.Substitute("${MY_VAR:-fallback}", _ => string.Empty);

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void Substitute_DefaultContainsColon_PreservesDefaultValue()
    {
        // Default values may themselves contain colons (but not newlines or closing braces).
        var result = EnvironmentSubstitution.Substitute("${MY_VAR:-http://localhost:9090}", _ => null);

        Assert.Equal("http://localhost:9090", result);
    }

    // Left-to-right $$ pair consumption, including chains longer than the spec table covers.
    [Theory]
    [InlineData("$$", "$")]
    [InlineData("$$$", "$$")] // escape then a lone trailing dollar
    [InlineData("$$$$", "$$")]
    [InlineData("$$$$${STRING_VALUE}", "$$hello")]
    public void Substitute_EscapeChain_MatchesExpectedOutput(string input, string expected)
    {
        var result = EnvironmentSubstitution.Substitute(
            input,
            name => name == "STRING_VALUE" ? "hello" : null);

        Assert.Equal(expected, result);
    }

    // Spec-documented behavior: default values are not recursively substituted.
    // ${A:-${B}} with A unset produces the literal string "${B}" - the closing '}' of the
    // inner expression satisfies the outer expression, and the remaining '}' leaks as a literal.
    // B is never evaluated. This matches the Java SDK and is explicit in the OTel spec table.
    [Fact]
    public void Substitute_NestedDefaultSubstitution_IsNotRecursive()
    {
        var result = EnvironmentSubstitution.Substitute(
            "${A:-${B}}",
            name => name == "A" ? null : "unreachable");

        Assert.Equal("${B}", result);
    }

    // A '${' with no closing '}' is not a SUBSTITUTION-REF (the grammar requires the '}'), so per
    // the spec it is literal text rather than an error. The spec's own
    // '${UNDEFINED_KEY:-$${UNDEFINED_KEY}}' row depends on this: it outputs a dangling
    // '${UNDEFINED_KEY:-' and is explicitly NOT an error.
    //
    // Regression guard: this used to throw at the end of the value but stay literal before a '$$',
    // so the same text behaved differently depending on what followed it.
    [Theory]
    [InlineData("${VAR", "${VAR")] // missing closing brace
    [InlineData("prefix ${VAR", "prefix ${VAR")] // missing closing brace mid-string
    [InlineData("${A:-${B", "${A:-${B")] // unterminated inside a default value context
    [InlineData("${VAR$$", "${VAR$")] // unterminated before an escape
    [InlineData("$${VAR", "${VAR")] // escape then unterminated
    [InlineData("${", "${")]
    public void Substitute_UnterminatedExpression_IsLeftAsLiteralText(string input, string expected) =>
        Assert.Equal(expected, EnvironmentSubstitution.Substitute(input, _ => "unused"));

    [Fact]
    public void Substitute_TerminatedTokenFollowedByUnterminated_ResolvesFirstAndKeepsSecondLiteral()
    {
        // ${A} is a complete reference; ${B has no closing brace and stays literal.
        var result = EnvironmentSubstitution.Substitute("${A} ${B", _ => "x");

        Assert.Equal("x ${B", result);
    }

    // A *complete* ${...} whose content is not a valid reference is an error: the spec requires the
    // parser to "return an empty result (no partial results are allowed) and an error".
    [Theory]
    [InlineData("${1API_KEY}")] // name starts with a digit
    [InlineData("${VAR:?error}")] // :? is not valid syntax (only :- is)
    [InlineData("${}")] // empty name
    [InlineData("${env:}")] // env: prefix with no name
    [InlineData("${1VAR:-default}")] // invalid first char before :-
    [InlineData("${MY.VAR:-default}")] // dot in name before :-
    [InlineData("${MY.VAR}")] // dot is not part of the ENV-NAME grammar
    [InlineData("${MY VAR}")] // space is not part of the ENV-NAME grammar
    [InlineData("${unknown_prefix:value}")] // a prefix other than env: is not supported
    public void Substitute_InvalidReference_ThrowsDeclarativeConfigurationException(string input) =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute(input, _ => null));

    // Regression: the diagnostic must name the offending expression. A bare character offset is not
    // enough to find the problem in a long or multi-line scalar.
    [Fact]
    public void Substitute_InvalidReference_MessageQuotesTheOffendingExpression()
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("endpoint=${GOOD}/${MY.VAR}/tail", name => "ok"));

        Assert.Contains("${MY.VAR}", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("${GOOD}", ex.Message, StringComparison.Ordinal);
    }

    // The spec ABNF VCHAR-WSP-NO-RBRACE permits only printable ASCII (excluding '}'),
    // tab, space, and '~'. Control characters such as \n, \r, and DEL (\x7F) are forbidden.
    [Theory]
    [InlineData("${VAR:-default\nvalue}")]
    [InlineData("${VAR:-default\rvalue}")]
    [InlineData("${VAR:-default\x7fvalue}")]
    [InlineData("${VAR:-caf\u00E9}")]
    public void Substitute_InvalidCharacterInDefaultValue_ThrowsDeclarativeConfigurationException(string input) =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute(input, _ => null));

    [Theory]
    [InlineData("${VAR:-\tvalue}", "\tvalue")] // TAB is WSP and therefore allowed
    [InlineData("${VAR:- spaced }", " spaced ")] // SPACE is WSP and therefore allowed
    [InlineData("${VAR:-~tilde}", "~tilde")] // '~' (U+007E) is explicitly allowed
    [InlineData("${VAR:-a|b}", "a|b")] // '|' (U+007C) is the top of the allowed range
    [InlineData("${VAR:-a$b}", "a$b")] // a lone '$' is an ordinary character
    public void Substitute_DefaultValueBoundaryCharacters_AreAccepted(string input, string expected) =>
        Assert.Equal(expected, EnvironmentSubstitution.Substitute(input, _ => null));

    // Regression: the default-value diagnostic used to identify neither the expression nor its
    // position, so a document with several references gave nothing to search for.
    [Fact]
    public void Substitute_InvalidDefaultValue_ThrowsWithDefaultValueMessageNamingTheExpression()
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${VAR:-caf\u00E9}", _ => null));

        Assert.Contains("default value", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("${VAR:-caf\u00E9}", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ENV_NAME", ex.Message, StringComparison.Ordinal);
    }

    // The expression is echoed as decoded text, so an offending control character or DEL is
    // invisible in the message and a non-ASCII one is indistinguishable from a legal character.
    // The code point and its offset are what make the diagnostic actionable.
    [Theory]
    [InlineData("${VAR:-a\nb}", "U+000A", 1)]
    [InlineData("${VAR:-a\u007Fb}", "U+007F", 1)]
    [InlineData("${VAR:-caf\u00E9}", "U+00E9", 3)]
    [InlineData("${VAR:-\u0085}", "U+0085", 0)]
    public void Substitute_InvalidDefaultValue_MessageNamesTheOffendingCodePointAndOffset(
        string input, string expectedCodePoint, int expectedOffset)
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute(input, _ => null));

        Assert.Contains(expectedCodePoint, ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"offset {expectedOffset} of the default value",
            ex.Message,
            StringComparison.Ordinal);
    }

    // A malformed default is an error even when the variable is set and the default is discarded:
    // validity must not depend on the ambient environment.
    [Fact]
    public void Substitute_InvalidDefaultValue_ThrowsEvenWhenVariableIsSet() =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${VAR:-caf\u00E9}", _ => "set"));

    // Substitution is a single pass: resolved values are never rescanned for further ${...}
    // expressions, so a cycle cannot recurse.
    [Fact]
    public void Substitute_CircularReference_SinglePassOnly()
    {
        var result = EnvironmentSubstitution.Substitute(
            "${A}",
            name => name switch
            {
                "A" => "${B}",
                "B" => "${A}",
                _ => null,
            });

        Assert.Equal("${B}", result);
    }

    [Fact]
    public void Substitute_ChainedReference_SinglePassOnly()
    {
        var result = EnvironmentSubstitution.Substitute(
            "${A} and ${B}",
            name => name switch
            {
                "A" => "${B}",
                "B" => "${C}",
                "C" => "${A}",
                _ => null,
            });

        Assert.Equal("${B} and ${C}", result);
    }

    // A resolved value containing '$$' must not be unescaped: escapes are resolved on the input
    // only. Complements the spec's VALUE_WITH_ESCAPE row for the multi-region case.
    [Fact]
    public void Substitute_ResolvedValueContainingEscape_IsNotUnescaped()
    {
        var result = EnvironmentSubstitution.Substitute("$$${A}", _ => "x$$y");

        Assert.Equal("$x$$y", result);
    }

    // Doubling every '$' must round-trip exactly: after escaping, no region can contain a '${', so
    // nothing is substituted and nothing is rejected. The fuzz suite asserts this over arbitrary
    // input; these are the cases most likely to expose an off-by-one in region splitting.
    [Theory]
    [InlineData("${VAR}")]
    [InlineData("${VAR:-default}")]
    [InlineData("$")]
    [InlineData("$$")]
    [InlineData("$$$")]
    [InlineData("a$b$c")]
    [InlineData("${A}${B}${C}")]
    [InlineData("${UNTERMINATED")]
    public void Substitute_EveryDollarEscaped_RoundTripsExactly(string original)
    {
#if NET
        ArgumentNullException.ThrowIfNull(original);
#else
        if (original is null)
        {
            throw new ArgumentNullException(nameof(original));
        }
#endif

#pragma warning disable CA1307 // Specify StringComparison for clarity - the 3-arg overload is not available on all TFMs
        var escaped = original.Replace("$", "$$");
#pragma warning restore CA1307 // Specify StringComparison for clarity

        Assert.Equal(original, EnvironmentSubstitution.Substitute(escaped, _ => "unused"));
    }

    [Fact]
    public void Substitute_NullValue_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => EnvironmentSubstitution.Substitute(null!, _ => null));

    [Fact]
    public void Substitute_NullResolver_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => EnvironmentSubstitution.Substitute("${VAR}", null!));

    private static string? ResolveSpecVariable(string name) =>
        SpecEnvironment.TryGetValue(name, out var value) ? value : null;
}
