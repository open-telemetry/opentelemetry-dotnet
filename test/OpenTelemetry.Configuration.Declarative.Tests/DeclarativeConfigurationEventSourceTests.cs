// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// AddOpenTelemetryDeclarativeConfiguration carries the OTEL1006 experimental attribute.
// Suppress once here rather than at each call site.
#pragma warning disable OTEL1006

using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationEventSourceTests
{
    [Fact]
    public void EventSource_ValidatesEventIds() =>
        EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetryDeclarativeConfigurationEventSource>();

    [Fact]
    public void ReadConfiguration_InvalidDisabledValue_EmitsInvalidBooleanValueWarning()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: yes
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 4);
        Assert.Equal("disabled", warning.Payload![0]);
        Assert.Equal("yes", warning.Payload[1]);
    }

    [Fact]
    public void ReadConfiguration_MalformedAttributesList_EmitsMalformedSectionWarning()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list:
                - service.name=my-service
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 6);
        Assert.Equal(YamlKeys.AttributesList, warning.Payload![0]);
        Assert.Contains("scalar", warning.Payload[1] as string, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadConfiguration_DuplicateResourceAttributeName_EmitsDuplicateNameWarning()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: first
                - name: service.name
                  value: second
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 18);
        Assert.Equal("service.name", warning.Payload![0]);
    }

    // Test events 15 and 16 by injecting a custom resolver into EnvironmentSubstitution.Substitute
    // rather than setting real env vars. On Windows, Environment.SetEnvironmentVariable("VAR", "")
    // removes the variable rather than setting it to empty, making it impossible to distinguish
    // "not set" from "set to empty" via the process environment.

    [Fact]
    public void Substitute_NullEnvVarNoDefault_EmitsEnvironmentVariableNotSetEvent()
    {
        using var listener = CreateVerboseListener();

        EnvironmentSubstitution.Substitute("${MY_NOTSET_VAR}", _ => null);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 15);
        Assert.Equal("MY_NOTSET_VAR", evt.Payload![0]);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == 16);
    }

    [Fact]
    public void Substitute_EmptyEnvVarNoDefault_EmitsEnvironmentVariableEmptyEvent()
    {
        using var listener = CreateVerboseListener();

        EnvironmentSubstitution.Substitute("${MY_EMPTY_VAR}", _ => string.Empty);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 16);
        Assert.Equal("MY_EMPTY_VAR", evt.Payload![0]);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == 15);
    }

    [Fact]
    public void Substitute_NullEnvVarWithDefault_EmitsNoVariableDiagnosticEvent()
    {
        using var listener = CreateVerboseListener();

        // When a default is present, neither event 15 nor event 20 fires even if the var is null/empty.
        EnvironmentSubstitution.Substitute("${MY_NOTSET_VAR:-fallback}", _ => null);
        EnvironmentSubstitution.Substitute("${MY_EMPTY_VAR:-fallback}", _ => string.Empty);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == 15);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == 20);
    }

    [Fact]
    public void ReadConfiguration_ResourceAttributeMappingValue_EmitsMappingNotRepresentableWarning()
    {
        // A 'value' that is a YAML mapping cannot be represented in OTEL_RESOURCE_ATTRIBUTES.
        // The warning must NOT say "missing required 'value' field" (which is wrong for this case).
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value:
                    nested: not-a-scalar
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        var message = warning.Payload![0] as string;
        Assert.Contains("mapping", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missing", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("scalar-string")] // scalar where a sequence is expected
    [InlineData("{ nested: value }")] // mapping where a sequence is expected
    public void ReadConfiguration_MalformedResourceAttributes_EmitsMalformedSectionWarning(string attributesValue)
    {
        // resource.attributes present but not a YAML sequence should emit MalformedSection (Event 6),
        // not silently return Absent.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes: {attributesValue}
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 6);
        Assert.Equal(YamlKeys.Attributes, warning.Payload![0]);
        Assert.Contains("sequence", warning.Payload[1] as string, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadConfiguration_ResourceAttributeUnknownType_EmitsUnknownTypeWarning()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  type: matrix
                  value: my-value
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        var message = warning.Payload![0] as string;
        Assert.Contains("unrecognized", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matrix", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadConfiguration_ResourceAttributeNullValue_EmitsNullValueWarning()
    {
        // value: ~ is present-but-null: distinct from a missing 'value' key. The diagnostic
        // must say "null" rather than "missing required 'value' field".
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  value: ~
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        var message = warning.Payload![0] as string;
        Assert.Contains("null", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missing", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("bool", "yes")]
    [InlineData("bool", "no")]
    [InlineData("int", "3.14")]
    [InlineData("double", "not-a-number")]
    public void ReadConfiguration_ResourceAttributeValueTypeMismatch_EmitsValueTypeMismatchWarning(string type, string value)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {type}
                  value: {value}
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 21);
        Assert.Equal("my.attr", warning.Payload![0]);
        Assert.Equal(type, warning.Payload[1]);
        Assert.Equal(value, warning.Payload[2]);
    }

    [Theory]
    [InlineData("bool", "true")]
    [InlineData("bool", "false")]
    [InlineData("bool", "True")]
    [InlineData("int", "42")]
    [InlineData("int", "-5")]
    [InlineData("double", "3.14")]
    [InlineData("double", "1e5")]
    [InlineData("string", "anything-goes")]
    public void ReadConfiguration_ResourceAttributeValueMatchesType_DoesNotEmitTypeMismatchWarning(string type, string value)
    {
        // Valid type/value pairs must not fire Event 21. Without this assertion a bug that always
        // fires Event 21 regardless of consistency would not be caught by the "should fire" theory.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
                  type: {type}
                  value: {value}
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == 21);
    }

    // B1: tiered name validation - hard-reject (Event 3) vs soft-warn (Event 22)

    [Theory]
    [InlineData("my=key")]
    [InlineData("my,key")]
    public void ReadConfiguration_ResourceAttributeHardInvalidName_EmitsInvalidResourceAttributeEvent(string name)
    {
        // Names containing '=' or ',' are hard-rejected (Event 3) because they would corrupt
        // the OTEL_RESOURCE_ATTRIBUTES flat key=value,key=value format.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: {name}
                  value: some-value
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 3);
        Assert.Contains(name, evt.Payload![0] as string, StringComparison.Ordinal);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == 22);
    }

    [Theory]
    [InlineData("1invalid")]
    [InlineData("my key")]
    public void ReadConfiguration_ResourceAttributeSoftNonConformingName_EmitsNameNotCompliantWarning(string name)
    {
        // Names that fail the naming convention but contain no ',' or '=' are emitted with
        // Event 22 (ResourceAttributeNameNotCompliant) rather than being hard-rejected.
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: "{name}"
                  value: some-value
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 22);
        Assert.Equal(name, evt.Payload![0]);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == 3);
    }

    [Fact]
    public void ReadConfiguration_ResourceAttributeConventionalName_DoesNotEmitNameNotCompliantWarning()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: my-service
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == 22);
    }

    // B2: empty file emits Event 23 at informational level

    [Fact]
    public void ReadConfiguration_EmptyFile_EmitsEmptyConfigurationFileEvent()
    {
        using var listener = CreateVerboseListener();

        _ = ReadConfiguration(string.Empty);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 23);
        Assert.NotNull(evt.Payload![0] as string);
    }

    [Fact]
    public void ReadConfiguration_EmptyFile_EmptyConfigurationFileEventNotEmittedBelowInformationalLevel()
    {
        // Event 23 is Informational; a Warning-only listener must not see it.
        using var listener = CreateWarningListener();

        _ = ReadConfiguration(string.Empty);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == 23);
    }

    // P3-F: verify Event 5 (MultipleDocumentsDetected) and Event 17 (OtelConfigFileNotSet) fire

    [Fact]
    public void ReadConfiguration_MultipleDocuments_EmitsMultipleDocumentsDetectedWarning()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: true
            ---
            file_format: "1.0"
            disabled: false
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 5);
        Assert.Equal(2, evt.Payload![0]);
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_Parameterless_NoEnvVar_EmitsOtelConfigFileNotSetWarning()
    {
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, null);
        using var listener = CreateWarningListener();

        new ConfigurationBuilder().AddOpenTelemetryDeclarativeConfiguration();

        Assert.Single(listener.Messages, e => e.EventId == 17);
    }

    private static TestEventListener CreateVerboseListener()
    {
        var listener = new TestEventListener();
        listener.EnableEvents(
            OpenTelemetryDeclarativeConfigurationEventSource.Log,
            EventLevel.Verbose,
            EventKeywords.All);
        return listener;
    }

    private static TestEventListener CreateWarningListener()
    {
        var listener = new TestEventListener();
        listener.EnableEvents(
            OpenTelemetryDeclarativeConfigurationEventSource.Log,
            EventLevel.Warning,
            EventKeywords.All);
        return listener;
    }

    private static ReadOnlyDictionary<string, string?> ReadConfiguration(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)));
    }
}
