// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationEventSourceTests
{
    [Fact]
    public void EventSource_ValidatesEventIds() =>
        EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetryDeclarativeConfigurationEventSource>();

    [Fact]
    public void ReadConfiguration_InvalidDisabledValue_ThrowsTypeErrorWithoutWarning()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: yes
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Empty(listener.Messages);
    }

    [Fact]
    public void ReadConfiguration_MalformedAttributesList_ThrowsTypeErrorWithoutWarning()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list:
                - service.name=my-service
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Empty(listener.Messages);
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
    public void ReadConfiguration_ResourceAttributeMappingValue_EmitsInvalidAttributeEvent()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value:
                    nested: not-a-scalar
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        Assert.Contains("mapping", warning.Payload![0] as string, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("scalar-string")] // scalar where a sequence is expected
    [InlineData("{ nested: value }")] // mapping where a sequence is expected
    public void ReadConfiguration_MalformedResourceAttributes_ThrowsTypeErrorWithoutWarning(string attributesValue)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes: {attributesValue}
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Empty(listener.Messages);
    }

    [Fact]
    public void ReadConfiguration_ResourceAttributeUnknownType_EmitsInvalidAttributeEvent()
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

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        Assert.Contains("matrix", warning.Payload![0] as string, StringComparison.Ordinal);
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

    [Fact]
    public void ReadConfiguration_NonStringResourceAttribute_EmitsLosslessProjectionWarning()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: integer.attribute
                  type: int
                  value: 42
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        var message = warning.Payload![0] as string;
        Assert.Contains("int", message, StringComparison.Ordinal);
        Assert.Contains("without losing its type", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadConfiguration_ResourceAttributeAbsentValue_EmitsInvalidAttributeEvent()
    {
        // Absent 'value' is distinct from present-null. The diagnostic must say "missing" not "null".
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: my.attr
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        var message = warning.Payload![0] as string;
        Assert.Contains("missing", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("bool", "yes")]
    [InlineData("bool", "no")]
    [InlineData("int", "3.14")]
    [InlineData("double", "not-a-number")]
    public void ReadConfiguration_ResourceAttributeValueTypeMismatch_EmitsInvalidAttributeEvent(string type, string value)
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

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var warning = Assert.Single(listener.Messages, e => e.EventId == 3);
        Assert.Contains(type, warning.Payload![0] as string, StringComparison.Ordinal);
    }

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

    [Fact]
    public void UseDeclarativeConfiguration_FactoryDescriptorReturnsNull_EmitsPriorConfigurationResolutionFailedWarning()
    {
        // Register an IConfiguration factory that returns null to exercise the path where
        // a descriptor was found but resolves to null at runtime (Event 19).
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(_ => null!);

        new TestEventSourceBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        using var listener = CreateWarningListener();

        var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

        Assert.Single(listener.Messages, e => e.EventId == 19);
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void ReadConfiguration_QuotedDisabledValue_ThrowsTypeErrorWithoutWarning()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: "true"
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Empty(listener.Messages);
    }

    // Mixed-case spellings are strings under the YAML 1.2 core schema, so they take the generic
    // invalid-boolean diagnostic rather than the quoting one.
    [Fact]
    public void ReadConfiguration_MixedCaseDisabledValue_ThrowsTypeErrorWithoutWarning()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: tRue
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Empty(listener.Messages);
    }

    [Fact]
    public void ReadConfiguration_QuotedDisabledFromUnsetEnvVar_ThrowsTypeErrorWithoutWarning()
    {
        const string envVarName = "OTEL_DECLARATIVE_TEST_EVENT_DISABLED_UNSET";
        const string yaml = """
            file_format: "1.0"
            disabled: "${OTEL_DECLARATIVE_TEST_EVENT_DISABLED_UNSET}"
            """;

        using var envScope = EnvironmentVariableScope.Create(envVarName, null);
        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Empty(listener.Messages);
    }

    [Fact]
    public void ReadConfiguration_UnquotedNumericAttributesList_ThrowsTypeErrorWithoutWarning()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: 1.5
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
        Assert.Empty(listener.Messages);
    }

    [Fact]
    public void ReadConfiguration_MergeLikeKeyAtRoot_IsIgnoredAsUnknownSection()
    {
        const string yaml = """
            defaults: &d
              disabled: true
            file_format: "1.0"
            <<: *d
            """;

        using var listener = CreateWarningListener();

        var data = ReadConfiguration(yaml);

        Assert.Empty(data);
        var events = listener.Messages.Where(e => e.EventId == 2).ToArray();
        Assert.Equal(2, events.Length);
        Assert.Contains(events, e => Equals(e.Payload![0], "defaults"));
        Assert.Contains(events, e => Equals(e.Payload![0], "<<"));
    }

    [Fact]
    public void ReadConfiguration_MergeLikeKeyInResource_ThrowsSchemaErrorAndEmitsUnknownPropertyEvent()
    {
        const string yaml = """
            defaults: &d
              attributes_list: service.name=from-merge
            file_format: "1.0"
            resource:
              <<: *d
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var evt = Assert.Single(listener.Messages, e => e.EventId == 25);
        Assert.Equal("resource.<<", evt.Payload![0]);
    }

    [Fact]
    public void ReadConfiguration_UnterminatedSubstitution_EmitsVerboseUnresolvedExpressionEvent()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: "service.name=${UNTERMINATED"
            """;

        using var listener = CreateVerboseListener();

        _ = ReadConfiguration(yaml);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 24);
        Assert.Equal("${UNTERMINATED", evt.Payload![0]);
    }

    [Theory]
    [InlineData("attributes_lsit", "resource.attributes_lsit")]
    public void ReadConfiguration_MisspelledResourceProperty_EmitsUnknownPropertyEvent(
        string misspelledKey, string expectedPath)
    {
        // The schema sets additionalProperties=false on Resource, so an unknown property key is a
        // schema violation and the parse must fail after reporting it.
        var yaml = $"""
            file_format: "1.1"
            resource:
              {misspelledKey}: "service.name=test"
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var evt = Assert.Single(listener.Messages, e => e.EventId == 25);
        Assert.Equal(expectedPath, evt.Payload![0]);
    }

    [Theory]
    [InlineData("vlaue", "resource.attributes[1].vlaue")]
    public void ReadConfiguration_MisspelledAttributeEntryProperty_EmitsUnknownPropertyEventWithIndex(
        string misspelledKey, string expectedPath)
    {
        var yaml = $"""
            file_format: "1.1"
            resource:
              attributes:
                - name: a
                  value: text
                - name: b
                  value: text
                  {misspelledKey}: typo
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var evt = Assert.Single(listener.Messages, e => e.EventId == 25);
        Assert.Equal(expectedPath, evt.Payload![0]);
    }

    [Fact]
    public void ReadConfiguration_SupportedResourceProperties_EmitNoPropertyDiagnostics()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes_list: "region=us-east-1"
              attributes:
                - name: a
                  type: string
                  value: text
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        Assert.DoesNotContain(listener.Messages, e => e.EventId is 25 or 26);
    }

    [Fact]
    public void ReadConfiguration_QuotedMergeLikeKeyAtRoot_IsLoggedAsUnknownSectionNotRejected()
    {
        // Under the YAML 1.2 core schema "<<" is an ordinary string key, and the top-level schema
        // object sets additionalProperties=true, so this document is legal.
        const string yaml = """
            file_format: "1.1"
            "<<": some-extension-value
            """;

        using var listener = CreateWarningListener();

        var data = ReadConfiguration(yaml);

        var evt = Assert.Single(listener.Messages, e => e.EventId == 2);
        Assert.Equal("<<", evt.Payload![0]);
        Assert.Empty(data);
    }

    [Fact]
    public void ReadConfiguration_StrTaggedMergeLikeKeyAtRoot_IsAccepted()
    {
        const string yaml = """
            file_format: "1.1"
            !!str << : some-extension-value
            """;

        using var listener = CreateWarningListener();

        _ = ReadConfiguration(yaml);

        Assert.Single(listener.Messages, e => e.EventId == 2);
    }

    [Fact]
    public void ReadConfiguration_PlainMergeLikeKeyAtRoot_IsLoggedAsUnknownSection()
    {
        const string yaml = """
            file_format: "1.1"
            <<: {x: 1}
            """;

        using var listener = CreateWarningListener();

        var data = ReadConfiguration(yaml);

        Assert.Empty(data);
        var evt = Assert.Single(listener.Messages, e => e.EventId == 2);
        Assert.Equal("<<", evt.Payload![0]);
    }

    [Fact]
    public void ReadConfiguration_PlainMergeLikeKeyInResource_IsRejectedAsUnknownProperty()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              <<: {x: 1}
            """;

        using var listener = CreateWarningListener();

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

        var evt = Assert.Single(listener.Messages, e => e.EventId == 25);
        Assert.Equal("resource.<<", evt.Payload![0]);
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

    private sealed class TestEventSourceBuilder : IOpenTelemetryBuilder
    {
        public TestEventSourceBuilder(IServiceCollection services)
        {
            this.Services = services;
        }

        public IServiceCollection Services { get; }
    }
}
