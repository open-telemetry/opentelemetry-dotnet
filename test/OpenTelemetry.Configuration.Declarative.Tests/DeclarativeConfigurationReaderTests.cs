// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationReaderTests
{
    [Fact]
    public void Translate_DisabledTrue_SetsOtelSdkDisabled()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: true
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("true", data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    [Fact]
    public void Translate_DisabledFalse_SetsOtelSdkDisabledFalse()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: false
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("false", data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    [Fact]
    public void Translate_DisabledAbsent_DoesNotSetKey()
    {
        const string yaml = """
            file_format: "1.0"
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
    }

    [Fact]
    public void Translate_SingleResourceAttribute_BuildsFlatString()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my-service
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("service.name=my-service", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_MultipleResourceAttributes_PreservesOrderWithCommaDelimiter()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my-service
                - name: service.version
                  value: 1.2.3
                - name: deployment.environment
                  value: production
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "service.name=my-service,service.version=1.2.3,deployment.environment=production",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_EmptyYaml_ProducesNoKeys()
    {
        // Intentional: an empty stream is a no-op and does not require file_format.
        // In overlay mode an empty/missing file contributes nothing so the SDK uses defaults.
        var data = ReadConfiguration(string.Empty);

        Assert.Empty(data);
    }

    [Fact]
    public void Translate_NonScalarTopLevelKey_Throws()
    {
        const string yaml = """
            file_format: "1.0"
            ? [a, b]
            : some_value
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_UnknownTopLevelSection_IsIgnoredWithoutThrowing()
    {
        const string yaml = """
            file_format: "1.0"
            tracer_provider:
              some_key: some_value
            propagator:
              composite: [tracecontext, baggage]
            """;

        // Must not throw; unknown sections are logged and ignored.
        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain("tracer_provider", data.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("propagator", data.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_EnvVarSubstitution_ResolvesValueFromEnvironment()
    {
        // Use a constant name so the YAML value can be a plain raw-string literal
        // (avoiding $"..." interpolation which would conflict with ${...} syntax).
        const string envVarName = "OTEL_DECLARATIVE_TEST_SVC_NAME";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: ${OTEL_DECLARATIVE_TEST_SVC_NAME}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "my-substituted-service");

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "service.name=my-substituted-service",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_EnvVarSubstitutionWithDefault_UsesDefaultWhenEnvVarUnset()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: ${OTEL_DECLARATIVE_TEST_MISSING_VAR:-fallback-service}
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "service.name=fallback-service",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_DoubleQuotedDefaultWithYamlNewlineEscape_IsRejected()
    {
        // Substitution runs on the decoded scalar, so YamlDotNet has already turned \n into a real
        // newline by the time DEFAULT-VALUE is validated. A newline is outside VCHAR-WSP-NO-RBRACE,
        // so this must fail: a YAML escape cannot smuggle an illegal character into a default. Only
        // '$$' hides a reference.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: note
                  value: "${OTEL_DECLARATIVE_TEST_DQ_NEWLINE_DEFAULT:-a\nb}"
            """;

        var ex = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("U+000A", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("caf\u00E9")] // non-ASCII (U+00E9)
    [InlineData(@"a\Nb")] // NEL (U+0085)
    [InlineData(@"a\_b")] // NBSP (U+00A0)
    [InlineData(@"a\x7Fb")] // DEL
    [InlineData(@"a\rb")] // carriage return
    [InlineData(@"a\0b")] // NUL
    public void Translate_DoubleQuotedDefaultWithEscapeOutsideDefaultAlphabet_IsRejected(string defaultValue)
    {
        // Every escape form is rejected consistently, because validation always sees decoded text.
        var yaml = $$"""
            file_format: "1.0"
            resource:
              attributes:
                - name: note
                  value: "${OTEL_DECLARATIVE_TEST_DQ_ESCAPE_DEFAULT:-{{defaultValue}}}"
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_DoubleQuotedDefaultWithTabEscape_IsAccepted()
    {
        // TAB is WSP and therefore a legal DEFAULT-VALUE character, so \t decodes and survives.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: note
                  value: "${OTEL_DECLARATIVE_TEST_DQ_TAB_DEFAULT:-a\tb}"
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("note=a\tb", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_DoubleQuotedDollarHexEscape_StillFormsAReference()
    {
        // "\x24" decodes to '$' before substitution, so a YAML escape cannot hide a reference.
        const string envVarName = "OTEL_DECLARATIVE_TEST_DQ_DOLLAR_ESCAPE";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: note
                  value: "\x24{OTEL_DECLARATIVE_TEST_DQ_DOLLAR_ESCAPE}"
            """;

        using var environment = EnvironmentVariableScope.Create(envVarName, "resolved");

        var data = ReadConfiguration(yaml);

        Assert.Equal("note=resolved", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_DoubleQuotedEnvValueWithLiteralBackslashN_IsNotYamlUnescaped()
    {
        // Env values are inserted verbatim and the result is never re-parsed as YAML, so a value
        // containing the characters '\' and 'n' stays those two characters.
        const string envVarName = "OTEL_DECLARATIVE_TEST_LITERAL_SLASH_N";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: note
                  value: "${OTEL_DECLARATIVE_TEST_LITERAL_SLASH_N}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "a\\nb");

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "note=a\\nb",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_MissingFileFormat_Throws()
    {
        const string yaml = """
            disabled: true
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_UnsupportedFileFormat_Throws()
    {
        const string yaml = """
            file_format: "2.0"
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceWithUnknownProperty_Throws()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              some_future_key: value
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceIsScalar_ThrowsTypeError()
    {
        const string yaml = """
            file_format: "1.0"
            resource: scalar-value
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceIsSequence_ThrowsTypeError()
    {
        // Malformed: resource: is a YAML sequence instead of a mapping.
        const string yaml = """
            file_format: "1.0"
            resource:
              - foo
              - bar
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_UnquotedFileFormat_ThrowsTypeError()
    {
        // YAML 1.2: plain (unquoted) '1.0' is a float, not a string. file_format must be quoted.
        const string yaml = """
            file_format: 1.0
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_UnquotedBooleanDisabled_IsRecognized()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: true
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("true", data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("1")]
    [InlineData("on")]
    public void Translate_NonBooleanDisabled_ThrowsTypeError(string value)
    {
        var yaml = $"""
            file_format: "1.0"
            disabled: {value}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeValueWithComma_IsUrlEncoded()
    {
        // The spec requires ',' and '=' to be percent-encoded in OTEL_RESOURCE_ATTRIBUTES values
        // so they do not corrupt the flat key=value,key=value format. OtelEnvResourceDetector
        // URL-decodes values via WebUtility.UrlDecode, which handles %XX sequences correctly.

        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: custom.attr
                  value: a,b
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("custom.attr=a%2Cb", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributeValueWithEquals_IsUrlEncoded()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: custom.attr
                  value: key=value
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("custom.attr=key%3Dvalue", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributeValueWithPercent_IsUrlEncoded()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: custom.attr
                  value: 50%
            """;

        var data = ReadConfiguration(yaml);

        // % must be encoded to prevent unexpected UrlDecode behaviour in OtelEnvResourceDetector.
        Assert.Equal("custom.attr=50%25", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Theory]
    [InlineData("my=key")] // equals sign corrupts flat format
    [InlineData("my,key")] // comma corrupts flat format
    public void Translate_ResourceAttributeHardInvalidName_IsSkipped(string name)
    {
        // Names containing '=' or ',' are hard-rejected: they would corrupt the flat
        // key=value,key=value format consumed by OtelEnvResourceDetector.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: {name}
                  value: some-value
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Theory]
    [InlineData("1invalid")] // starts with digit
    [InlineData("my key")] // contains space
    public void Translate_ResourceAttributeSoftNonConformingName_IsEmittedVerbatim(string name)
    {
        // Names that fail the naming convention but contain no ',' or '=' are emitted
        // as-is (soft warn, Event 22). The flat format is not corrupted by these names.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: "{name}"
                  value: some-value
            """;

        var data = ReadConfiguration(yaml);

        Assert.True(data.ContainsKey(DeclarativeConfigurationConverter.ResourceAttributesKey));
        Assert.Contains(name, data[DeclarativeConfigurationConverter.ResourceAttributesKey], StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_ResourceAttributeValidNameFollowedBySoftNonConformingName_BothAreEmitted()
    {
        // A conventional name and a soft-non-conforming name (starts with digit) are both emitted.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my-service
                - name: "1invalid"
                  value: also-emitted
            """;

        var data = ReadConfiguration(yaml);

        var attrs = data[DeclarativeConfigurationConverter.ResourceAttributesKey];
        Assert.Contains("service.name=my-service", attrs, StringComparison.Ordinal);
        Assert.Contains("1invalid=also-emitted", attrs, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_MultipleDocuments_ProcessesOnlyFirstDocument()
    {
        // A YAML stream with more than one document should log a warning and
        // process only the first document.

        const string yaml = """
            file_format: "1.0"
            disabled: true
            ---
            file_format: "1.0"
            disabled: false
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("true", data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    [Theory]
    [InlineData("TRUE", "true")]
    [InlineData("FALSE", "false")]
    public void Translate_DisabledFromEnvVarSubstitution_NormalizesToCanonicalLowercase(
        string envVarValue, string expected)
    {
        // Disabled value arriving via env-var substitution is resolved using the YAML core schema.

        const string envVarName = "OTEL_DECLARATIVE_TEST_DISABLED_CASE";
        const string yaml = """
            file_format: "1.0"
            disabled: ${OTEL_DECLARATIVE_TEST_DISABLED_CASE}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, envVarValue);

        var data = ReadConfiguration(yaml);
        Assert.Equal(expected, data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    [Fact]
    public void Translate_QuotedFileFormatFromSetEnvVar_ValidatesResolvedValue()
    {
        // Quoting forces string interpretation after environment-variable substitution.

        const string envVarName = "OTEL_DECLARATIVE_TEST_FORMAT_VERSION";
        const string yaml = """
            file_format: "${OTEL_DECLARATIVE_TEST_FORMAT_VERSION}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "1.0");

        var data = ReadConfiguration(yaml);
        Assert.Empty(data);
    }

    [Fact]
    public void Translate_UnquotedNumericFileFormatFromSetEnvVar_ThrowsTypeError()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_NUMERIC_FORMAT_VERSION";
        const string yaml = """
            file_format: ${OTEL_DECLARATIVE_TEST_NUMERIC_FORMAT_VERSION}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "1.0");

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_FileFormatFromUnsetEnvVar_ThrowsMissingFieldMessage()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_FORMAT_MISSING";
        const string yaml = """
            file_format: ${OTEL_DECLARATIVE_TEST_FORMAT_MISSING}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);

        var ex = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Contains("file_format", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"{FileFormatValidator.SupportedMajorVersion}.0", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"{FileFormatValidator.SupportedMajorVersion}.{FileFormatValidator.MaxSupportedMinorVersion}", ex.Message, StringComparison.Ordinal);
    }

    // Round-trip tests: mirror OtelEnvResourceDetector (trim the value segment, then UrlDecode).
    // The encoder percent-encodes '%', ',', '=', '+', and leading/trailing whitespace so surrounding
    // whitespace survives that trim; interior whitespace and all other characters pass through as-is.
    [Theory]
    [InlineData("a+b")] // + is encoded as %2B, decoded back to +
    [InlineData("foo bar")] // internal space passes through unencoded; Trim only strips edges
    [InlineData(" leading")] // leading whitespace must survive detector Trim
    [InlineData("trailing ")] // trailing whitespace must survive detector Trim
    [InlineData(" both ")]
    [InlineData("\tleading\t")] // tab is whitespace and must be encoded
    [InlineData("50%")] // % is encoded as %25
    [InlineData("key=val")] // = is encoded as %3D
    [InlineData("a,b")] // , is encoded as %2C
    [InlineData("http://x:9090")] // other special chars pass through unencoded
    public void Translate_ResourceAttributeValue_RoundTripsThroughUrlDecode(string originalValue)
    {
        Guard.ThrowIfNull(originalValue);

        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: "{EscapeYamlDoubleQuoted(originalValue)}"
            """;

        var data = ReadConfiguration(yaml);

        var flatValue = data[DeclarativeConfigurationConverter.ResourceAttributesKey];
        var encodedValue = flatValue!.Split(['='], 2)[1];
        var decoded = DecodeResourceAttributeValue(encodedValue);
        Assert.Equal(originalValue, decoded);
    }

    [Fact]
    public void Translate_ResourceAttributeQuotedEmptyValue_RoundTripsThroughUrlDecode()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: ""
            """;

        var data = ReadConfiguration(yaml);

        var flatValue = data[DeclarativeConfigurationConverter.ResourceAttributesKey];
        var encodedValue = flatValue!.Split(['='], 2)[1];
        var decoded = DecodeResourceAttributeValue(encodedValue);
        Assert.Equal(string.Empty, decoded);
    }

    [Fact]
    public void Translate_ResourceAttributeUnsetEnvVarPlainValue_IsSkipped()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_RESOURCE_ATTR_UNSET";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: ${OTEL_DECLARATIVE_TEST_RESOURCE_ATTR_UNSET}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourceAttributeUnsetEnvVarQuotedValue_EmitsEmptyString()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_RESOURCE_ATTR_QUOTED_UNSET";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: "${OTEL_DECLARATIVE_TEST_RESOURCE_ATTR_QUOTED_UNSET}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);

        var data = ReadConfiguration(yaml);

        Assert.Equal("my.attr=", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Theory]
    [InlineData("'~'", "~")]
    [InlineData("'null'", "null")]
    public void Translate_ResourceAttributeQuotedNullLikeValue_EmitsString(string yamlValue, string expectedValue)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: {yamlValue}
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal($"my.attr={expectedValue}", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_MultipleResourceAttributes_AllRoundTripThroughUrlDecode()
    {
        // Verifies that the comma separator between attributes is not confused with
        // an encoded comma inside any individual value, and that each value decodes correctly.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my+service
                - name: deployment.environment
                  value: prod,staging
                - name: custom.percent
                  value: 100%
            """;

        var data = ReadConfiguration(yaml);

        var flat = data[DeclarativeConfigurationConverter.ResourceAttributesKey]!;
        var pairs = flat.Split(',');
        Assert.Equal(3, pairs.Length);

        static string DecodeValue(string pair) =>
            DecodeResourceAttributeValue(pair.Split(['='], 2)[1]);

        Assert.Equal("my+service", DecodeValue(pairs[0]));
        Assert.Equal("prod,staging", DecodeValue(pairs[1]));
        Assert.Equal("100%", DecodeValue(pairs[2]));
    }

    [Fact]
    public void Translate_DisabledIsMapping_ThrowsTypeError()
    {
        const string yaml = """
            file_format: "1.0"
            disabled:
              some_key: some_value
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_DisabledIsSequence_ThrowsTypeError()
    {
        const string yaml = """
            file_format: "1.0"
            disabled:
              - true
              - false
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_DuplicateResourceAttributeNames_FirstWins()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: first-value
                - name: service.name
                  value: second-value
            """;

        var data = ReadConfiguration(yaml);

        // first-wins: only the first occurrence is emitted
        Assert.Equal("service.name=first-value", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_DuplicateResourceAttributeNameAmongMultiple_EmitsOnlyFirstOccurrenceOfDuplicate()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my-service
                - name: env
                  value: prod
                - name: service.name
                  value: duplicate-ignored
            """;

        var data = ReadConfiguration(yaml);

        var flat = data[DeclarativeConfigurationConverter.ResourceAttributesKey]!;
        Assert.Contains("service.name=my-service", flat, StringComparison.Ordinal);
        Assert.Contains("env=prod", flat, StringComparison.Ordinal);
        Assert.DoesNotContain("duplicate-ignored", flat, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_EmptyTopLevelKey_DoesNotThrow()
    {
        // Empty string key (''). Must not throw; no output should be produced for it.
        const string yaml = """
            file_format: "1.0"
            '': some_value
            """;

        var data = ReadConfiguration(yaml);

        Assert.Empty(data);
    }

    [Fact]
    public void Translate_NullTopLevelKey_Throws()
    {
        const string yaml = """
            file_format: "1.0"
            ~: some_value
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeMappingValue_Throws()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value:
                    nested: not-a-scalar
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("")]
    public void Translate_ResourceAttributeNullValue_IsSkipped(string nullValue)
    {
        // The null entry is skipped; a valid sibling in the same attributes block must still be emitted.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: {nullValue}
                - name: service.name
                  value: my-service
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("service.name=my-service", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributeMissingValue_ThrowsWithoutPartialResult()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                - name: service.name
                  value: my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeNonMappingSequenceItem_ThrowsWithoutPartialResult()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - not-a-mapping
                - name: service.name
                  value: my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("")]
    public void Translate_ResourceAttributeNullName_Throws(string nullName)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: {nullName}
                  value: my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeAbsentName_ThrowsWithoutPartialResult()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - value: orphaned-value
                - name: service.name
                  value: my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_DuplicateTopLevelKeys_ThrowsYamlException()
    {
        // YamlDotNet's RepresentationModel rejects duplicate keys at parse time.
        // The YamlException propagates out of Translate; callers that surface this to
        // users (e.g. DeclarativeConfigurationProvider.Load) must catch and wrap it.
        const string yaml = """
            file_format: "1.0"
            disabled: false
            disabled: true
            """;

        Assert.Throws<YamlDotNet.Core.YamlException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("!!str disabled")]
    [InlineData("! disabled")]
    public void Translate_TagEquivalentDuplicateTopLevelKeys_Throws(string duplicateKey)
    {
        var yaml = $"""
            file_format: "1.0"
            disabled: false
            {duplicateKey}: true
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_RootNotMapping_Throws()
    {
        const string yaml = """
            - not
            - a
            - mapping
            """;

        var ex = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Contains("mapping", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_ResourceAttributesList_IsPassedThroughAsIs()
    {
        // attributes_list is a pre-encoded OTEL_RESOURCE_ATTRIBUTES-format string. It is
        // passed through without additional encoding after environment-variable substitution.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "service.name=my-service,service.version=1.2.3"
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "service.name=my-service,service.version=1.2.3",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributesList_WithEnvironmentSubstitution_IsResolved()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_ATTRS_LIST";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: ${OTEL_DECLARATIVE_TEST_ATTRS_LIST}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "service.name=svc,region=us-east-1");

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "service.name=svc,region=us-east-1",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributesList_Empty_ProducesNoKey()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: ""
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Theory]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("${OTEL_DECLARATIVE_TEST_ATTRS_LIST_UNSET}")]
    public void Translate_ResourceAttributesList_PlainNull_ProducesNoKey(string nullValue)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes_list: {nullValue}
            """;

        using var envScope = EnvironmentVariableScope.Create("OTEL_DECLARATIVE_TEST_ATTRS_LIST_UNSET", null);

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourceAttributesList_QuotedUnsetEnvVarNoDefault_ProducesNoKey()
    {
        // A quoted '${VAR}' with no default: GetScalarString returns "" (non-null, because the
        // DoubleQuoted style suppresses YAML-null inference). The ReadString empty check must
        // still treat this as present-null rather than passing an empty list to the projector.
        const string envVarName = "OTEL_DECLARATIVE_TEST_ATTRS_LIST_QUOTED_UNSET";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "${OTEL_DECLARATIVE_TEST_ATTRS_LIST_QUOTED_UNSET}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourceAttributesList_NonScalar_ThrowsTypeError()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list:
                - service.name=my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributesAndAttributesList_AttributesKeyWinsWithNoDuplicates()
    {
        // When both fields are present and share a key, the attributes entry wins and the
        // attributes_list entry for that key is filtered out. The output contains each key
        // exactly once; non-overlapping attributes_list entries are preserved.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "service.name=from-list,region=us-east-1"
              attributes:
                - name: service.name
                  value: from-attributes
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "region=us-east-1,service.name=from-attributes",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributesAndAttributesList_WhitespaceInAttributeNameStillShadowsList()
    {
        // OtelEnvResourceDetector trims flat-format keys. The precedence comparison must therefore
        // use the same normalization, otherwise the lower-priority list entry survives and wins.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: region=from-list
              attributes:
                - name: "region "
                  value: from-attributes
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "region =from-attributes",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributesAndAttributesList_AllListKeysOverridden_EmitsOnlyAttributes()
    {
        // When every key in attributes_list is also present in attributes, the filtered
        // attributes_list is empty and the output contains only the attributes entries.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "service.name=from-list"
              attributes:
                - name: service.name
                  value: from-attributes
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "service.name=from-attributes",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Theory]
    [InlineData("int", "3")]
    [InlineData("bool", "true")]
    [InlineData("double", "1.5")]
    public void Translate_UnsupportedScalarTypeInAttributes_StillShadowsAttributesList(
        string type, string value)
    {
        // resource.attributes outranks resource.attributes_list, so a declared name must suppress
        // the list entry even when this projection cannot carry the higher-priority value. Falling
        // back to the list value would silently emit the lower-priority string "5".
        var yaml = $"""
            file_format: "1.1"
            resource:
              attributes_list: retry.count=5
              attributes:
                - name: retry.count
                  type: {type}
                  value: {value}
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_UnsupportedArrayTypeInAttributes_StillShadowsAttributesList()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes_list: tags=from-list
              attributes:
                - name: tags
                  type: string_array
                  value: [a, b]
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_UnsupportedTypeShadowing_AppliesWhenAnotherAttributeProjects()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes_list: "retry.count=5,region=us-east-1"
              attributes:
                - name: retry.count
                  type: int
                  value: 3
                - name: keep
                  value: kept
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "region=us-east-1,keep=kept",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_NullAttributeValue_DoesNotShadowAttributesList()
    {
        // Schema v1.1: "Property must be present, but if null the entry is ignored." An ignored
        // entry declares nothing, so the lower-priority attributes_list value stands.
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes_list: note=from-list
              attributes:
                - name: note
                  value: null
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("note=from-list", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_UnsupportedTypeThenSameNameString_ProjectsTheStringEntry()
    {
        // The shadowing set and the first-wins duplicate set must stay separate: reserving the name
        // for the skipped int entry must not make the later projectable entry look like a duplicate.
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes:
                - name: x
                  type: int
                  value: 3
                - name: x
                  value: str
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("x=str", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_NullValueThenSameNameString_ProjectsTheStringEntry()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes:
                - name: x
                  value: null
                - name: x
                  value: str
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("x=str", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_DuplicateStringAttributeNames_FirstStillWins()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes:
                - name: x
                  value: first
                - name: x
                  value: second
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("x=first", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributesList_EncodedCommaInValue_PassesThroughUnchanged()
    {
        // attributes_list is pre-encoded OTEL_RESOURCE_ATTRIBUTES format. FilterAttributesList
        // splits on literal ',' only, so %2C inside a value must not be treated as a delimiter.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "description=hello%2Cworld,region=us-east-1"
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "description=hello%2Cworld,region=us-east-1",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributesList_EncodedCommaInValue_RoundTripsThroughUrlDecode()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "description=hello%2Cworld"
            """;

        var data = ReadConfiguration(yaml);

        var flat = data[DeclarativeConfigurationConverter.ResourceAttributesKey]!;
        var encodedValue = flat.Split(['='], 2)[1];
        Assert.Equal("hello,world", System.Net.WebUtility.UrlDecode(encodedValue));
    }

    [Fact]
    public void Translate_ResourceAttributesAndAttributesList_FilterPreservesEncodedCommaInListValue()
    {
        // When attributes_list and attributes are merged, FilterAttributesList must not split
        // on %2C inside an attributes_list value while removing the overlapping key.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "description=hello%2Cworld,service.name=from-list,region=us-east-1"
              attributes:
                - name: service.name
                  value: from-attributes
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "description=hello%2Cworld,region=us-east-1,service.name=from-attributes",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);

        var flat = data[DeclarativeConfigurationConverter.ResourceAttributesKey]!;
        var descriptionPair = flat.Split(',')[0];
        var encodedValue = descriptionPair.Split(['='], 2)[1];
        Assert.Equal("hello,world", System.Net.WebUtility.UrlDecode(encodedValue));
    }

    [Fact]
    public void Translate_ResourceAttributesList_UnencodedCommaInValue_SplitsAtComma()
    {
        // Documented limitation: attributes_list is comma-split naively (same as
        // OtelEnvResourceDetector). Unencoded commas inside a value corrupt the flat format.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "description=hello,world"
            """;

        var data = ReadConfiguration(yaml);

        // Parsed as two malformed entries: description=hello and world (no '=').
        Assert.Equal("description=hello,world", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributeArrayValue_IsSkipped()
    {
        // Array-typed attribute values (e.g. string_array) cannot be represented in the flat
        // OTEL_RESOURCE_ATTRIBUTES key=value format. The entry is skipped; other valid entries
        // in the same attributes block are still emitted.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my-service
                - name: my.hosts
                  type: string_array
                  value:
                    - host1
                    - host2
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("service.name=my-service", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Theory]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    public void Translate_DisabledPresentNull_DoesNotSetKey(string nullValue)
    {
        // All YAML 1.2 core schema null spellings are present-but-null. Per the spec this is distinct
        // from absent and from an invalid value: it selects the field's null behaviour (here, the
        // default), so no key is emitted and it is NOT reported as an invalid boolean.
        var yaml = $"""
            file_format: "1.0"
            disabled: {nullValue}
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
    }

    [Fact]
    public void Translate_DisabledPresentEmpty_DoesNotSetKey()
    {
        // 'disabled:' with no value is a null scalar; treated as present-null, not invalid.
        const string yaml = """
            file_format: "1.0"
            disabled:
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
    }

    [Fact]
    public void Translate_DisabledFromUnsetEnvVarNoDefault_DoesNotSetKey()
    {
        // An unset '${VAR}' with no default substitutes to empty, which resolves to present-null
        // rather than an invalid boolean value.
        const string envVarName = "OTEL_DECLARATIVE_TEST_DISABLED_UNSET";
        const string yaml = """
            file_format: "1.0"
            disabled: ${OTEL_DECLARATIVE_TEST_DISABLED_UNSET}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
    }

    [Fact]
    public void Translate_DisabledFromQuotedUnsetEnvVarNoDefault_ThrowsTypeError()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_DISABLED_QUOTED_UNSET";
        const string yaml = """
            file_format: "1.0"
            disabled: "${OTEL_DECLARATIVE_TEST_DISABLED_QUOTED_UNSET}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourcePresentNull_ThrowsSchemaError()
    {
        const string yaml = """
            file_format: "1.0"
            resource: ~
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    // type field handling (fix 2.3)

    [Fact]
    public void Translate_ResourceAttributeStringType_EmitsAttribute()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: string
                  value: some-value
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("my.attr=some-value", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Theory]
    [InlineData("string_array")]
    [InlineData("bool_array")]
    [InlineData("int_array")]
    [InlineData("double_array")]
    public void Translate_ResourceAttributeArrayTypeWithScalarValue_Throws(string arrayType)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {arrayType}
                  value: scalar-value
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeUnknownType_Throws()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  type: matrix
                  value: my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeUnknownTypeWithValidSibling_ThrowsWithoutPartialResult()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my-service
                - name: bad.attr
                  type: unknown_type
                  value: skipped
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeNestedMappingValue_Throws()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value:
                    nested: not-a-scalar
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    // Sequence value without type field

    [Fact]
    public void Translate_ResourceAttributeSequenceValueNoType_ThrowsWithoutPartialResult()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.list
                  value:
                    - item1
                    - item2
                - name: service.name
                  value: my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    // Type-value consistency

    [Theory]
    [InlineData("bool", "yes")]
    [InlineData("bool", "no")]
    [InlineData("int", "3.14")]
    [InlineData("double", "not-a-number")]
    public void Translate_ResourceAttributeValueTypeMismatch_Throws(string type, string value)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {type}
                  value: {value}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeNullType_Throws()
    {
        // AttributeType's enum excludes null, so null cannot select the default string type.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: ~
                  value: my-value
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_ResourceAttributeMappingValueWithValidSibling_ThrowsWithoutPartialResult()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: bad.attr
                  value:
                    nested: not-a-scalar
                - name: service.name
                  value: my-service
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("bool", "true")]
    [InlineData("bool", "false")]
    [InlineData("bool", "True")]
    [InlineData("int", "42")]
    [InlineData("int", "-5")]
    [InlineData("double", "3.14")]
    [InlineData("double", "1e5")]
    [InlineData("double", "-0.5")]
    public void Translate_ResourceAttributeValidNonStringScalar_IsNotProjectedAsString(string type, string value)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {type}
                  value: {value}
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    // M4: plain ${VAR} resolving to a YAML null spelling (null/NULL/~) -> present-null.
    // Distinct from unset ${VAR} (which resolves to empty): here the env var IS set,
    // but its value is a YAML 1.2 core schema null token.

    [Theory]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("~")]
    public void Translate_DisabledFromEnvVarSetToNullLiteral_DoesNotSetKey(string envVarValue)
    {
        // When an env var is set to a YAML null spelling, substitution produces a plain
        // scalar with that value. Yaml12ScalarResolver.ResolvesToNull treats it as
        // present-null -> no key emitted.
        const string envVarName = "OTEL_DECLARATIVE_TEST_DISABLED_NULL_LITERAL";
        const string yaml = """
            file_format: "1.0"
            disabled: ${OTEL_DECLARATIVE_TEST_DISABLED_NULL_LITERAL}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, envVarValue);

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("~")]
    public void Translate_ResourceAttributeValueFromEnvVarSetToNullLiteral_IsSkipped(string envVarValue)
    {
        // Same null-via-substitution path for a resource attribute value field.
        const string envVarName = "OTEL_DECLARATIVE_TEST_ATTR_VALUE_NULL_LITERAL";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: ${OTEL_DECLARATIVE_TEST_ATTR_VALUE_NULL_LITERAL}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, envVarValue);

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    // YAML 1.2: a quoted scalar is a string, so it cannot satisfy the boolean 'disabled' field.
    [Theory]
    [InlineData("\"true\"")]
    [InlineData("'true'")]
    [InlineData("\"false\"")]
    public void Translate_QuotedBooleanDisabled_ThrowsTypeError(string value)
    {
        var yaml = $"""
            file_format: "1.0"
            disabled: {value}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("tRue")]
    [InlineData("TrUe")]
    [InlineData("truE")]
    [InlineData("fALSE")]
    public void Translate_MixedCaseBooleanDisabled_ThrowsTypeError(string value)
    {
        var yaml = $"""
            file_format: "1.0"
            disabled: {value}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void Translate_Yaml12BooleanSpellings_AreRecognized(string value)
    {
        var yaml = $"""
            file_format: "1.0"
            disabled: {value}
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("true", data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    // An explicit YAML tag overrides core schema resolution (YAML 1.2 section 3.2.1.2).
    [Fact]
    public void Translate_ExplicitStringTagOnUnquotedFileFormat_IsAccepted()
    {
        const string yaml = """
            file_format: !!str 1.0
            """;

        var data = ReadConfiguration(yaml);

        Assert.Empty(data);
    }

    [Fact]
    public void Translate_ExplicitBooleanTagOnQuotedDisabled_IsRecognized()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: !!bool "true"
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("true", data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    // Aliases are resolved by YamlDotNet to the anchored node itself, so the anchored node's style
    // is what the readers see. A quoted anchor must not become a boolean via an alias, and a plain
    // anchor must still work.
    [Fact]
    public void Translate_AliasToQuotedBoolean_ThrowsTypeErrorLikeTheAnchor()
    {
        const string yaml = """
            file_format: "1.0"
            anchors:
              quoted: &q "true"
            disabled: *q
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_AliasToPlainBoolean_IsRecognizedLikeTheAnchor()
    {
        const string yaml = """
            file_format: "1.0"
            anchors:
              plain: &p true
            disabled: *p
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("true", data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    [Fact]
    public void Translate_AliasToPlainNumericFileFormat_ThrowsTypeErrorLikeTheAnchor()
    {
        // The alias must not launder a plain float into a string.
        const string yaml = """
            anchors:
              version: &v 1.0
            file_format: *v
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_UndefinedAlias_FailsToLoad()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: *nope
            """;

        // YamlDotNet raises AnchorNotFoundException during Load; it is a YamlException, which the
        // provider wraps. The reader surfaces it directly.
        Assert.ThrowsAny<Exception>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_CyclicAliasInUnknownSection_IsIgnoredWithoutRecursingIndefinitely()
    {
        const string yaml = """
            file_format: "1.0"
            extension: &cycle
              self: *cycle
            """;

        Assert.Empty(ReadConfiguration(yaml));
    }

    // Under the YAML 1.2 core schema, << is an ordinary string key. At the extension-permitting
    // root it is ignored; it is not expanded as a YAML 1.1 merge key.
    [Fact]
    public void Translate_MergeLikeKeyAtRoot_IsIgnored()
    {
        const string yaml = """
            defaults: &d
              disabled: true
            file_format: "1.0"
            <<: *d
            """;

        Assert.Empty(ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_MergeLikeKeyInResource_ThrowsUnknownPropertyError()
    {
        const string yaml = """
            defaults: &d
              attributes:
                - name: from.merge
                  value: nope
            file_format: "1.0"
            resource:
              <<: *d
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_UnquotedNumericAttributesList_ThrowsTypeError()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: 1.5
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void Translate_QuotedNumericAttributesList_IsAccepted()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "service.version=1.5"
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("service.version=1.5", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    // Type resolution happens after substitution: an unquoted reference takes the type of whatever
    // the variable resolved to. '1.0' and '0xdeadbeef' are numbers, 'true' is a boolean.
    [Theory]
    [InlineData("1.0")]
    [InlineData("true")]
    [InlineData("0xdeadbeef")]
    public void Translate_SubstitutedUnquotedFileFormat_ResolvingToNonString_ThrowsTypeError(string envValue)
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_SUBST_TYPE";
        const string yaml = """
            file_format: ${OTEL_DECLARATIVE_TEST_SUBST_TYPE}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, envValue);

        var ex = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("must resolve to string or null", ex.Message, StringComparison.Ordinal);
    }

    // '1.x' is not a YAML 1.2 number, so it stays a string and reaches format validation instead.
    // This separates "wrong type" from "wrong value" and pins that the two diagnostics differ.
    [Fact]
    public void Translate_SubstitutedUnquotedFileFormat_ResolvingToString_ReachesFormatValidation()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_SUBST_TYPE_STR";
        const string yaml = """
            file_format: ${OTEL_DECLARATIVE_TEST_SUBST_TYPE_STR}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "1.x");

        var ex = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("Unsupported file_format '1.x'", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("must resolve to string or null", ex.Message, StringComparison.Ordinal);
    }

    // Quoting forces a string on every path, so the same variable value that fails unquoted works.
    [Fact]
    public void Translate_SubstitutedQuotedFileFormat_ResolvingToNumber_IsAccepted()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_SUBST_TYPE_QUOTED";
        const string yaml = """
            file_format: "${OTEL_DECLARATIVE_TEST_SUBST_TYPE_QUOTED}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "1.0");

        Assert.Empty(ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("TRUE", "true")]
    [InlineData("false", "false")]
    public void Translate_SubstitutedDisabled_ResolvesBooleanAfterSubstitution(string envValue, string expected)
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_SUBST_DISABLED";
        const string yaml = """
            file_format: "1.0"
            disabled: ${OTEL_DECLARATIVE_TEST_SUBST_DISABLED}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, envValue);

        var data = ReadConfiguration(yaml);

        Assert.Equal(expected, data[DeclarativeConfigurationConverter.DisabledKey]);
    }

    [Theory]
    [InlineData("tRue")]
    [InlineData("yes")]
    [InlineData("1")]
    public void Translate_SubstitutedDisabledWithNonYaml12Boolean_ThrowsTypeError(string envValue)
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_SUBST_DISABLED_BAD";
        const string yaml = """
            file_format: "1.0"
            disabled: ${OTEL_DECLARATIVE_TEST_SUBST_DISABLED_BAD}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, envValue);

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    // A quoted reference is a string on every path, so it can never become a boolean.
    [Fact]
    public void Translate_QuotedSubstitutedDisabled_ThrowsTypeError()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_SUBST_DISABLED_QUOTED";
        const string yaml = """
            file_format: "1.0"
            disabled: "${OTEL_DECLARATIVE_TEST_SUBST_DISABLED_QUOTED}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "true");

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    // Substitution cannot inject YAML syntax. Surrounding whitespace therefore remains part of a
    // plain string instead of being trimmed into a null or numeric token.
    [Fact]
    public void Translate_SubstitutedValueWithSurroundingWhitespace_RemainsString()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_PADDED_NULL";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: ${OTEL_DECLARATIVE_TEST_PADDED_NULL}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "  null  ");

        var data = ReadConfiguration(yaml);

        Assert.Equal("my.attr=%20%20null%20%20", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_SubstitutedFileFormatWithSurroundingWhitespace_RemainsInvalidString()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_PADDED_NUMBER";
        const string yaml = """
            file_format: ${OTEL_DECLARATIVE_TEST_PADDED_NUMBER}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "  1.0  ");

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("Unsupported file_format '  1.0  '", exception.Message, StringComparison.Ordinal);
    }

    // An unterminated '${' is literal text, not an error, so a document containing one still loads.
    [Fact]
    public void Translate_UnterminatedSubstitutionInAttributeValue_IsLiteralText()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: "${UNTERMINATED"
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal(
            "my.attr=${UNTERMINATED",
            data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    private static string EscapeYamlDoubleQuoted(string value)
    {
        var firstIndex = value.IndexOfAny(['\\', '"']);
        if (firstIndex < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 4);
        builder.Append(value, 0, firstIndex);

        for (var i = firstIndex; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch is '\\' or '"')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    // Matches OtelEnvResourceDetector: trim the value segment, then URL-decode.
    private static string DecodeResourceAttributeValue(string encodedValue) =>
        System.Net.WebUtility.UrlDecode(encodedValue.Trim());

    private static ReadOnlyDictionary<string, string?> ReadConfiguration(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)));
    }
}
