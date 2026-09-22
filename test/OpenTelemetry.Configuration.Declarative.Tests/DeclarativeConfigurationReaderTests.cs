// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    public void Translate_SkippedAttributeEntry_DoesNotSuppressAttributesList()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes_list: retry.count=5
              attributes:
                - name: retry.count
                  type: int
                  value: 3
            """;

        var data = ReadConfiguration(yaml);

        Assert.Equal("retry.count=5", data[DeclarativeConfigurationConverter.ResourceAttributesKey]);
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
        // scalar with that value. YamlScalarResolver.ResolvesToNull treats it as
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
    public void Translate_CyclicAliasInUnknownSection_FailsToLoad()
    {
        const string yaml = """
            file_format: "1.0"
            extension: &cycle
              self: *cycle
            """;

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("<root>.extension.self", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_MergeLikeKeyAtRoot_FailsBeforeConfigurationIsInterpreted()
    {
        const string yaml = """
            defaults: &d
              disabled: true
            <<: *d
            """;

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("<root>.<<", exception.Message, StringComparison.Ordinal);
        Assert.Contains("YAML 1.1 merge key", exception.Message, StringComparison.Ordinal);
        Assert.Contains("line", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_MergeLikeKeyInResource_FailsBeforeSchemaValidation()
    {
        const string yaml = """
            defaults: &d
              attributes:
                - name: from.merge
                  value: yes-please
            file_format: "1.0"
            resource:
              <<: *d
            """;

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("<root>.resource.<<", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not supported by this declarative configuration implementation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_MergeLikeKeyInUninterpretedSection_FailsToLoad()
    {
        const string yaml = """
            file_format: "1.0"
            vendor:
              <<: value
            """;

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("<root>.vendor.<<", exception.Message, StringComparison.Ordinal);
    }

    // Only YAML 1.1 merge syntax is rejected. Spellings that resolve to a YAML 1.2 string remain
    // ordinary property names, which is the way to author a literal `<<` key.
    [Theory]
    [InlineData("\"<<\"")]
    [InlineData("!!str <<")]
    [InlineData("! <<")]
    public void Translate_NonPlainMergeLikeKeyInResource_IsTreatedAsAnOrdinaryKey(string key)
    {
        var yaml = $$"""
            file_format: "1.0"
            resource:
              {{key}} : {x: 1}
            """;

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Contains("resource.<<", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_ExplicitlyTaggedMergeKey_FailsToLoad()
    {
        const string yaml = """
            file_format: "1.0"
            !!merge "<<": value
            """;

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        Assert.Contains("<root>.<<", exception.Message, StringComparison.Ordinal);
        Assert.Contains("YAML 1.1 merge key", exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public void Translate_ResourceWithSchemaUrl_DoesNotThrow()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              schema_url: "https://opentelemetry.io/schemas/1.28.0"
            """;

        var data = ReadConfiguration(yaml);

        Assert.Empty(data);
    }

    [Fact]
    public void Translate_ResourceWithUnrecognizedKey_Throws()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              detection: {}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    private static ReadOnlyDictionary<string, string?> ReadConfiguration(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml))).FlatKeys;
    }
}
