// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class EnvironmentSubstitutionTests
{
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
    public void Substitute_DefaultUsedWhenUndefined()
    {
        var result = EnvironmentSubstitution.Substitute("${UNDEFINED_VAR:-my-default}", _ => null);

        Assert.Equal("my-default", result);
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

    // Tests for multi-escape chains.
    // These document the expected left-to-right $$ pair consumption behaviour
    // and serve as regression guards against regex changes.
    [Theory]
    [InlineData("$$", "$")] // single escape
    [InlineData("$$$$", "$$")] // two escapes -> two literal dollars
    [InlineData("$${STRING_VALUE}", "${STRING_VALUE}")] // escape prevents substitution
    [InlineData("$$${STRING_VALUE}", "$hello")] // one escape then substitution -> $hello
    [InlineData("$$$${STRING_VALUE}", "$${STRING_VALUE}")] // two escapes + literal braces (no $ at pos 4 to trigger substitution)
    [InlineData("$$$$${STRING_VALUE}", "$$hello")] // two escapes + substitution -> $$hello
    public void Substitute_SpecEscapeChain_MatchesExpectedOutput(string input, string expected)
    {
        var result = EnvironmentSubstitution.Substitute(
            input,
            name => name == "STRING_VALUE" ? "hello" : null);

        Assert.Equal(expected, result);
    }

    // Spec-documented behavior: default values are not recursively substituted.
    // ${A:-${B}} with A unset produces the literal string "${B}" - the closing '}' of the
    // inner expression satisfies the outer pattern, and the remaining '}' leaks as a literal.
    // B is never evaluated. This matches the Java SDK and is explicit in the OTel spec table.
    [Fact]
    public void Substitute_NestedDefaultSubstitution_IsNotRecursive()
    {
        var result = EnvironmentSubstitution.Substitute(
            "${A:-${B}}",
            name => name == "A" ? null : "unreachable");

        Assert.Equal("${B}", result);
    }

    // Unterminated ${ sequences must throw, not silently pass through as literals.
    // The OTel spec requires a hard error for syntactically invalid substitution expressions.
    [Theory]
    [InlineData("${VAR")] // missing closing brace
    [InlineData("prefix ${VAR")] // missing closing brace mid-string
    [InlineData("${A:-${B")] // unterminated inside default value context
    public void Substitute_UnterminatedExpression_ThrowsDeclarativeConfigurationException(string input) =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute(input, _ => null));

    // Invalid ${...} references must throw, not silently pass through.
    // The OTel spec requires a hard error for syntactically invalid substitution expressions.
    [Theory]
    [InlineData("${1API_KEY}")] // name starts with a digit (not allowed)
    [InlineData("${VAR:?error}")] // :? is not valid syntax (only :- is)
    [InlineData("${}")] // empty name
    [InlineData("${1VAR:-default}")] // invalid first char before :- (exercises HasValidEnvName -> false on first char)
    [InlineData("${MY.VAR:-default}")] // dot in name before :- (exercises HasValidEnvName -> false on subsequent char)
    public void Substitute_InvalidReference_ThrowsDeclarativeConfigurationException(string input) =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute(input, _ => null));

    // Dots are not part of the OTel ENV-NAME grammar and must be rejected.
    [Fact]
    public void Substitute_VarNameWithDot_ThrowsDeclarativeConfigurationException() =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${MY.VAR}", _ => "value"));

    // ${env:} has no variable name after the prefix. The valid-substitution
    // branch requires at least one letter or underscore, so it falls to the catch-all and throws.
    [Fact]
    public void Substitute_EmptyEnvPrefixName_ThrowsDeclarativeConfigurationException() =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${env:}", _ => null));

    // $$ must unescape to $ inside default values per the OTel spec, the same as at
    // the top level. The regex captures the raw default; the caller unescapes it before use.
    [Theory]
    [InlineData("${VAR:-he$$o}", "he$o")] // single $$ pair inside default
    [InlineData("${VAR:-$$}", "$")] // default consisting entirely of $$
    [InlineData("${VAR:-a$$b$$c}", "a$b$c")] // multiple $$ pairs inside default
    [InlineData("${VAR:-$$$$}", "$$")] // two $$ pairs -> two literal dollars
    public void Substitute_DoubleDollarInDefault_UnescapesToSingleDollar(string input, string expected)
    {
        var result = EnvironmentSubstitution.Substitute(input, _ => null);

        Assert.Equal(expected, result);
    }

    // The spec ABNF VCHAR-WSP-NO-RBRACE permits only printable ASCII (excluding '}'),
    // tab, space, and '~'. Control characters such as \n, \r, and DEL (\x7F) are forbidden.
    [Fact]
    public void Substitute_NewlineInDefaultValue_ThrowsDeclarativeConfigurationException() =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${VAR:-default\nvalue}", _ => null));

    [Fact]
    public void Substitute_CarriageReturnInDefaultValue_ThrowsDeclarativeConfigurationException() =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${VAR:-default\rvalue}", _ => null));

    [Fact]
    public void Substitute_DelInDefaultValue_ThrowsDeclarativeConfigurationException() =>
        Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${VAR:-default\x7fvalue}", _ => null));

    // Non-ASCII characters (e.g. accented letters) in a default value must produce a diagnostic
    // that points at the default value, not at the variable name. Before this fix the error said
    // "ENV_NAME starts with a letter or underscore" even when the name was perfectly valid.
    [Fact]
    public void Substitute_NonAsciiInDefaultValue_ThrowsWithDefaultValueMessage()
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${VAR:-caf\u00e9}", _ => null));

        Assert.Contains("default value", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ENV_NAME", ex.Message, StringComparison.Ordinal);
    }

    // Regression: the pre-scan used a global IndexOf('}') which found the closing brace of a
    // terminated token ahead of the unterminated one. "${A} ${B" must throw "unterminated" for
    // the ${B token, not silently treat ${B} as something else.
    [Fact]
    public void Substitute_TerminatedTokenFollowedByUnterminated_ThrowsUnterminated()
    {
        // ${A} is a valid token; ${B has no closing brace and must produce an "unterminated" error.
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => EnvironmentSubstitution.Substitute("${A} ${B", _ => "x"));

        Assert.Contains("unterminated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
