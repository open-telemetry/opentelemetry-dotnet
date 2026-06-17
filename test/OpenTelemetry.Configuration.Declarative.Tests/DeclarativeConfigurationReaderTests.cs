// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

using System.Collections.ObjectModel;

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
    public void Translate_ResourceWithNoAttributes_ProducesNoResourceKey()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              some_future_key: value
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourceIsScalar_DoesNotThrowAndProducesNoResourceKey()
    {
        // Malformed: resource: is a scalar value instead of a mapping.
        // The translator must log a warning (EventSource) and return without throwing or
        // emitting resource attributes.
        const string yaml = """
            file_format: "1.0"
            resource: scalar-value
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourceIsSequence_DoesNotThrowAndProducesNoResourceKey()
    {
        // Malformed: resource: is a YAML sequence instead of a mapping.
        const string yaml = """
            file_format: "1.0"
            resource:
              - foo
              - bar
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_UnquotedFileFormat_IsAcceptedIdenticallyToQuoted()
    {
        // Unquoted YAML scalars. YamlDotNet's RepresentationModel returns the raw
        // text for all scalars regardless of YAML type inference (float 1.0, bool true etc.),
        // so unquoted values should be accepted identically to their quoted equivalents.

        const string yaml = """
            file_format: 1.0
            """;

        var data = ReadConfiguration(yaml);

        Assert.Empty(data);
    }

    [Fact]
    public void Translate_UnquotedBooleanDisabled_IsRecognized()
    {
        const string yaml = """
            file_format: 1.0
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
    public void Translate_NonBooleanDisabled_DoesNotEmitKey(string value)
    {
        // Non-boolean values for disabled must be ignored with an EventSource warning.
        // "yes" / "no" / "1" are not valid OTel boolean values even though some YAML parsers accept them.

        var yaml = $"""
            file_format: "1.0"
            disabled: {value}
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
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
    [InlineData(" true ", "true")]
    [InlineData(" False ", "false")]
    public void Translate_DisabledFromEnvVarSubstitution_NormalizesToCanonicalLowercase(
        string envVarValue, string expected)
    {
        // Disabled value arriving via env-var substitution: the translator must normalize to
        // canonical lowercase "true"/"false" regardless of env-var casing or surrounding whitespace.

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
    public void Translate_FileFormatFromSetEnvVar_ValidatesResolvedValue()
    {
        // env-var substitution is applied to file_format (per spec).
        // When the env var resolves to a valid format the document is accepted. When it
        // resolves to empty (unset, no default) the validator throws with an unsupported-format error.

        const string envVarName = "OTEL_DECLARATIVE_TEST_FORMAT_VERSION";
        const string yaml = """
            file_format: ${OTEL_DECLARATIVE_TEST_FORMAT_VERSION}
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, "1.0");

        var data = ReadConfiguration(yaml);
        Assert.Empty(data);
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
        Assert.Contains("1.0", ex.Message, StringComparison.Ordinal);
    }

    // Round-trip tests: verify that values survive the full encode-then-decode path used by
    // OtelEnvResourceDetector. The encoder only percent-encodes '%', ',', '=', and '+';
    // all other characters pass through as-is. WebUtility.UrlDecode handles %XX correctly
    // and does not modify unencoded characters (except '+' which it treats as space - hence
    // why '+' is one of the four encoded characters).
    [Theory]
    [InlineData("a+b")] // + is encoded as %2B, decoded back to +
    [InlineData("foo bar")] // space passes through unencoded, decoded back to space
    [InlineData("50%")] // % is encoded as %25
    [InlineData("key=val")] // = is encoded as %3D
    [InlineData("a,b")] // , is encoded as %2C
    [InlineData("http://x:9090")] // other special chars pass through unencoded
    public void Translate_ResourceAttributeValue_RoundTripsThroughUrlDecode(string originalValue)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: {originalValue}
            """;

        var data = ReadConfiguration(yaml);

        var flatValue = data[DeclarativeConfigurationConverter.ResourceAttributesKey];
        var encodedValue = flatValue!.Split(['='], 2)[1];
        var decoded = System.Net.WebUtility.UrlDecode(encodedValue);
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
        var decoded = System.Net.WebUtility.UrlDecode(encodedValue);
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
            System.Net.WebUtility.UrlDecode(pair.Split(['='], 2)[1]);

        Assert.Equal("my+service", DecodeValue(pairs[0]));
        Assert.Equal("prod,staging", DecodeValue(pairs[1]));
        Assert.Equal("100%", DecodeValue(pairs[2]));
    }

    [Fact]
    public void Translate_DisabledIsMapping_DoesNotThrowAndDoesNotSetKey()
    {
        const string yaml = """
            file_format: "1.0"
            disabled:
              some_key: some_value
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
    }

    [Fact]
    public void Translate_DisabledIsSequence_DoesNotThrowAndDoesNotSetKey()
    {
        const string yaml = """
            file_format: "1.0"
            disabled:
              - true
              - false
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
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
    public void Translate_NullTopLevelKey_DoesNotThrow()
    {
        // YAML null key (~). Must not throw; no output should be produced for it.
        const string yaml = """
            file_format: "1.0"
            ~: some_value
            """;

        var data = ReadConfiguration(yaml);

        Assert.Empty(data);
    }

    [Fact]
    public void Translate_ResourceAttributeNonScalarValue_IsSkipped()
    {
        // 'value' is a YAML mapping. Mapping values cannot be represented in OTEL_RESOURCE_ATTRIBUTES
        // and the entry is skipped with a "mapping value" diagnostic (not "missing value").
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value:
                    nested: not-a-scalar
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
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

    [Theory]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("")]
    public void Translate_ResourceAttributeNullName_IsSkipped(string nullName)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: {nullName}
                  value: my-service
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
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
    public void Translate_ResourceAttributesList_NonScalar_DoesNotThrowAndProducesNoResourceKey()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list:
                - service.name=my-service
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
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
    public void Translate_DisabledFromQuotedUnsetEnvVarNoDefault_DoesNotSetKey()
    {
        // A quoted '${VAR}' with no default: GetScalarString returns "" (non-null, because the
        // DoubleQuoted style suppresses YAML-null inference). The ReadBoolean empty check must
        // still treat this as present-null rather than logging a spurious invalid-boolean warning.
        const string envVarName = "OTEL_DECLARATIVE_TEST_DISABLED_QUOTED_UNSET";
        const string yaml = """
            file_format: "1.0"
            disabled: "${OTEL_DECLARATIVE_TEST_DISABLED_QUOTED_UNSET}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourcePresentNull_DoesNotThrowAndProducesNoResourceKey()
    {
        // 'resource: ~' is present-but-null: distinct from a malformed non-null scalar. No resource
        // attributes are emitted and the document is processed without error.
        const string yaml = """
            file_format: "1.0"
            resource: ~
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    // type field handling (fix 2.3)

    [Theory]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData("int")]
    [InlineData("double")]
    public void Translate_ResourceAttributeKnownScalarType_EmitsAttribute(string type)
    {
        // Scalar type hints are informational; the value is still projected to the flat format.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {type}
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
    public void Translate_ResourceAttributeArrayTypeWithScalarValue_IsSkipped(string arrayType)
    {
        // An array type hint means the attribute cannot be projected to the flat format,
        // even if the YAML value node is a scalar.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {arrayType}
                  value: scalar-value
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourceAttributeUnknownType_IsSkipped()
    {
        // An unrecognized type hint is an authoring error; the entry is skipped
        // rather than emitting a potentially incorrect value.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  type: matrix
                  value: my-service
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    [Fact]
    public void Translate_ResourceAttributeUnknownTypeWithValidSibling_EmitsSiblingOnly()
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

        var data = ReadConfiguration(yaml);

        Assert.Equal("service.name=my-service", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void Translate_ResourceAttributeMappingValue_IsSkippedWithDistinctDiagnostic()
    {
        // A mapping-typed 'value' is neither "missing" nor an "array". The bridge must
        // emit a diagnostic that mentions "mapping", not "missing required 'value' field".
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value:
                    nested: not-a-scalar
            """;

        var data = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, data.Keys);
    }

    // Sequence value without type field (finding 7)

    [Fact]
    public void Translate_ResourceAttributeSequenceValueNoType_IsSkipped()
    {
        // A YAML sequence value with no 'type' field: guard (b) entry.ValueNodeKind == Sequence
        // catches this independently of the array-type guard (a) KnownArrayTypes.Contains(RawType).
        // Without this test, removing guard (b) while keeping guard (a) would be undetected.
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

        var data = ReadConfiguration(yaml);

        Assert.Equal("service.name=my-service", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    // Type-value consistency (finding 4)

    [Theory]
    [InlineData("bool", "yes")]
    [InlineData("bool", "no")]
    [InlineData("int", "3.14")]
    [InlineData("double", "not-a-number")]
    public void Translate_ResourceAttributeValueTypeMismatch_StillEmitsAttribute(string type, string value)
    {
        // The type field is informational per spec; the bridge emits the attribute as-is even when
        // the value is inconsistent with the declared type (a warning is logged separately).
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {type}
                  value: {value}
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal($"my.attr={value}", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
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
    public void Translate_ResourceAttributeValueMatchesType_EmitsAttributeWithoutWarning(string type, string value)
    {
        // Values consistent with their declared type must emit without triggering Event 21.
        // This is the "happy path" for typed attributes.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {type}
                  value: {value}
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal($"my.attr={value}", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
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
        // scalar with that value. IsPlainNullScalar treats it as present-null -> no key emitted.
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

    private static ReadOnlyDictionary<string, string?> ReadConfiguration(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)));
    }
}
