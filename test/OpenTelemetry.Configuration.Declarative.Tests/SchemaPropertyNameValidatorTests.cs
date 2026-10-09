// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Text;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class SchemaPropertyNameValidatorTests
{
    private const int UndefinedPropertyEventId = 25;
    private const int UndefinedRootPropertyEventId = 42;
    private const int UndefinedPropertyRetainedEventId = 43;
    private const int FileFormatWarningEventId = 1;

    private static readonly FileFormatVersion CurrentFormat = new("1.2", 1, 2);
    private static readonly FileFormatVersion NewerFormat = new("1.3", 1, 3);

    public static TheoryData<string, string> UndefinedStableKeyCases => new()
    {
        {
            """
            file_format: "1.2"
            tracer_provider:
              procesors: []
            """,
            "tracer_provider.procesors@3:3"
        },
        {
            """
            file_format: "1.2"
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      otlp_http:
                        endpont: x
            """,
            "tracer_provider.processors[0].batch.exporter.otlp_http.endpont@7:13"
        },
        {
            """
            file_format: "1.2"
            resource:
              foo: bar
            """,
            "resource.foo@3:3"
        },
        {
            """
            file_format: "1.2"
            resource:
              attributes:
                - name: a
                  value: b
                  foo: c
            """,
            "resource.attributes[0].foo@6:7"
        },
        {
            """
            file_format: "1.2"
            meter_provider:
              views:
                - stream:
                    name: n
                    nope: 1
            """,
            "meter_provider.views[0].stream.nope@6:9"
        },
        {
            """
            file_format: "1.2"
            resource:
              foo: 1
              bar: 2
            """,
            "resource.foo@3:3;resource.bar@4:3"
        },
        {
            """
            file_format: "1.2"
            resource:
              foo: 1
              bar: 2
            tracer_provider:
              procesors: []
            """,
            "resource.foo@3:3;resource.bar@4:3;tracer_provider.procesors@6:3"
        },
        {
            """
            file_format: "1.2"
            &key foo: 1
            resource:
              bar: 1
              *key : 2
            """,
            "resource.bar@4:3;resource.foo@5:3"
        },
        {
            """
            file_format: "1.2"
            vendor_keys: [&first foo, &second baz]
            resource:
              *first : {nested: [1, {item: 2}]}
              bar: [{nested: {item: 3}}]
              *second : *first
              last: 1
            """,
            "resource.foo@4:3;resource.bar@5:3;resource.baz@6:3;resource.last@7:3"
        },
        {
            """
            file_format: "1.2"
            log_level: &key foo
            resource:
              *key : 1
              bar: 2
            tracer_provider:
              *key : 3
              baz: 4
            """,
            "resource.foo@4:3;resource.bar@5:3;tracer_provider.foo@7:3;tracer_provider.baz@8:3"
        },
        {
            "file_format: \"1.2\"\nlog_level: &key foo\nresource:\n  *key : 1\n  attributes: []\n",
            "resource.foo@4:3"
        },
        {
            "file_format: \"1.2\"\nlog_level: &key foo\nresource:\n  attributes:\n    - name: service.name\n      value: example\n  *key : 1\n",
            "resource.foo@7:3"
        },
        {
            "file_format: \"1.2\"\nlog_level: &key foo\nresource: {*key : 1, attributes: [{name: service.name, value: example}]}\n",
            "resource.foo@3:12"
        },
        {
            "file_format: \"1.2\"\nlog_level: &key foo\ntracer_provider:\n  processors:\n    - batch:\n        exporter: {console: {}}\n        *key : 1\n",
            "tracer_provider.processors[0].batch.foo@7:9"
        },
        {
            "file_format: \"1.2\"\nlog_level: &key foo\ntracer_provider: {processors: [{batch: {exporter: {console: {}}, *key : 1}}]}\n",
            "tracer_provider.processors[0].batch.foo@3:66"
        },
        {
            "file_format: \"1.2\"\nresource:\n  \"\": 1\n",
            "resource.<empty>@3:3"
        },
        {
            "file_format: \"1.2\"\n!!str resource:\n  foo: 1\n",
            "resource.foo@3:3"
        },
    };

    [Theory]
    [InlineData((int)UndefinedPropertyKind.Unknown)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void ReportRetained_UnknownOrUnrecognizedKind_Throws(int value)
    {
        var kind = (UndefinedPropertyKind)value;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SchemaPropertyNameValidator.ReportRetained([new("resource.foo", kind)], CurrentFormat));

        Assert.Equal($"Unhandled UndefinedPropertyKind: {kind}.", exception.Message);
    }

    [Fact]
    public void ReportRetained_DefaultProperty_Throws() =>
        _ = Assert.Throws<InvalidOperationException>(() =>
            SchemaPropertyNameValidator.ReportRetained([default], CurrentFormat));

    [Fact]
    public void ReportRetained_WritesTheEventMatchingEachKind()
    {
        using var listener = CreateListener();

        SchemaPropertyNameValidator.ReportRetained(
            [
                new("a", UndefinedPropertyKind.Root),
                new("b", UndefinedPropertyKind.Experimental),
                new("c", UndefinedPropertyKind.NewerFileFormat),
            ],
            NewerFormat);

        var root = Assert.Single(Events(listener, UndefinedRootPropertyEventId));
        Assert.Equal(EventLevel.Informational, root.Level);
        Assert.Equal("a", root.Payload![0]);
        Assert.Equal(
            [
                ["b", "1.3", ConfigurationSchema.Version, SchemaPropertyNameValidator.ExperimentalReason],
                ["c", "1.3", ConfigurationSchema.Version, SchemaPropertyNameValidator.NewerFileFormatReason],
            ],
            RetainedPayloads(listener));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void Validate_UnrecognizedAdditionalPropertiesKind_Throws(int value)
    {
        var kind = (AdditionalPropertiesKind)value;
        var root = new SchemaNode { AdditionalProperties = kind };
        var properties = new ConfigPropertiesBuilder().Add("unknown", "value").Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SchemaPropertyNameValidator.Validate(properties, CurrentFormat, root));

        Assert.Equal($"Unhandled AdditionalPropertiesKind: {kind}.", exception.Message);
    }

    [Theory]
    [InlineData(2, "future/development", (int)UndefinedPropertyKind.Experimental)]
    [InlineData(3, "future/development", (int)UndefinedPropertyKind.NewerFileFormat)]
    [InlineData(3, "typo", (int)UndefinedPropertyKind.NewerFileFormat)]
    public void Validate_RetainedKey_ReportsTheReasonWithNewerFileFormatTakingPrecedence(
        int minor,
        string key,
        int expected)
    {
        var root = ObjectNode(AdditionalPropertiesKind.Allowed, ("resource", ObjectNode(AdditionalPropertiesKind.Forbidden)));
        var properties = Mapping("resource", new ConfigPropertiesBuilder().Add(key, "x").Build());

        var retained = SchemaPropertyNameValidator.Validate(properties, new($"1.{minor}", 1, minor), root);

        Assert.Equal([new UndefinedProperty($"resource.{key}", (UndefinedPropertyKind)expected)], retained);
    }

    [Fact]
    public void Validate_UndefinedKeyAtTheRoot_IsRetainedAsRoot()
    {
        var root = ObjectNode(AdditionalPropertiesKind.Allowed);
        var properties = new ConfigPropertiesBuilder().Add("vendor", "x").Build();

        var retained = SchemaPropertyNameValidator.Validate(properties, CurrentFormat, root);

        Assert.Equal([new UndefinedProperty("vendor", UndefinedPropertyKind.Root)], retained);
    }

    [Fact]
    public void Validate_UnderAdditionalPropertiesSchema_ExperimentalKeysAreRetainedAndStableKeysRejected()
    {
        var values = ObjectNode(AdditionalPropertiesKind.Forbidden);
        var map = new SchemaNode
        {
            ValueTypes = SchemaValueTypes.Object,
            AdditionalProperties = AdditionalPropertiesKind.Schema,
            AdditionalPropertiesSchema = values,
        };
        var root = ObjectNode(AdditionalPropertiesKind.Allowed, ("map", map));
        var typo = new ConfigPropertiesBuilder().Add("typo", "x").Build();

        var retained = SchemaPropertyNameValidator.Validate(
            Mapping("map", Mapping("a/development", typo)),
            CurrentFormat,
            root);

        Assert.Equal([new UndefinedProperty("map.a/development.typo", UndefinedPropertyKind.Experimental)], retained);

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            SchemaPropertyNameValidator.Validate(Mapping("map", Mapping("a", typo)), CurrentFormat, root));
        Assert.StartsWith("Property 'map.a.typo'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NodeWithoutTypes_IsWalked()
    {
        var root = ObjectNode(
            AdditionalPropertiesKind.Allowed,
            ("section", new SchemaNode { AdditionalProperties = AdditionalPropertiesKind.Forbidden }));

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            SchemaPropertyNameValidator.Validate(Mapping("section", Typo()), CurrentFormat, root));

        Assert.StartsWith("Property 'section.typo'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NodeWithAlternatives_IsNotWalked()
    {
        var root = ObjectNode(
            AdditionalPropertiesKind.Allowed,
            ("section", new SchemaNode { HasOneOf = true, AdditionalProperties = AdditionalPropertiesKind.Forbidden }));

        var retained = SchemaPropertyNameValidator.Validate(Mapping("section", Typo()), CurrentFormat, root);

        Assert.Empty(retained);
    }

    [Fact]
    public void Validate_SequenceOfMappings_ReportsTheIndexedPath()
    {
        var root = ObjectNode(AdditionalPropertiesKind.Allowed, ("section", ArrayNode(ObjectNode(AdditionalPropertiesKind.Forbidden))));
        var properties = new ConfigPropertiesBuilder()
            .Add("section", ConfigValue.Sequence([ConfigValue.Mapping(Typo())]))
            .Build();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            SchemaPropertyNameValidator.Validate(properties, CurrentFormat, root));

        Assert.StartsWith("Property 'section[0].typo'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sequenceForObject")]
    [InlineData("mappingForArray")]
    [InlineData("scalarForObject")]
    public void Validate_ValueWithTheWrongShape_IsNotWalked(string shape)
    {
        var objectNode = ObjectNode(AdditionalPropertiesKind.Forbidden);
        var (node, value) = shape switch
        {
            "sequenceForObject" => (objectNode, ConfigValue.Sequence([ConfigValue.Mapping(Typo())])),
            "mappingForArray" => (ArrayNode(objectNode), ConfigValue.Mapping(Typo())),
            _ => (objectNode, ConfigValue.String("x")),
        };
        var root = ObjectNode(AdditionalPropertiesKind.Allowed, ("section", node));
        var properties = new ConfigPropertiesBuilder().Add("section", value).Build();

        var retained = SchemaPropertyNameValidator.Validate(properties, CurrentFormat, root);

        Assert.Empty(retained);
    }

    [Fact]
    public void Read_ResourceDetectionDevelopment_Loads()
    {
        const string yaml = """
            file_format: "1.2"
            resource:
              detection/development:
                attributes:
                  included:
                    - process.*
                  excluded:
                    - process.command_args
                detectors:
                  - container:
                  - host:
                  - service:
              attributes:
                - name: service.name
                  value: my-service
            """;

        using var listener = CreateListener();

        var document = Read(yaml);

        Assert.True(document.Properties.GetMapping("resource").TryGetValue(out var resource), "Expected 'resource' mapping to contain a value.");
        Assert.Contains("detection/development", resource.Keys);
        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId is UndefinedPropertyEventId or UndefinedPropertyRetainedEventId);
    }

    [Theory]
    [MemberData(nameof(UndefinedStableKeyCases))]
    public void Read_UndefinedStableKeys_ThrowForTheFirstAndReportEachInDocumentOrder(string yaml, string expected)
    {
        var entries = Entries(expected);
        using var listener = CreateListener();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => Read(yaml));

        Assert.Equal(ExpectedMessage(entries[0], entries.Length - 1), exception.Message);
        Assert.Equal(entries, UndefinedKeys(listener));
        Assert.All(Events(listener, UndefinedPropertyEventId), e => Assert.Equal(EventLevel.Error, e.Level));
    }

    [Theory]
    [InlineData(
        "file_format: \"1.3\"\nlog_level: &key foo\nresource:\n  bar: 1\n  *key : 2\n",
        UndefinedPropertyRetainedEventId,
        "resource.bar;resource.foo")]
    [InlineData(
        "file_format: \"1.2\"\nlog_level: &key foo\nresource:\n  detection/development:\n    bar: 1\n    *key : 2\n",
        UndefinedPropertyRetainedEventId,
        "resource.detection/development.bar;resource.detection/development.foo")]
    [InlineData(
        "file_format: \"1.2\"\nlog_level: &key vendor_last\nvendor_first: {}\n*key : 1\n",
        UndefinedRootPropertyEventId,
        "vendor_first;vendor_last")]
    public void Read_AliasedRetainedKeys_AreReportedInDocumentOrder(string yaml, int eventId, string expected)
    {
        using var listener = CreateListener();

        _ = Read(yaml);

        Assert.Equal(
            Entries(expected),
            Events(listener, eventId).Select(e => (string)e.Payload![0]!));
    }

    [Fact]
    public void Read_UndefinedRootKey_IsRetainedAndReportedAtInformationalLevel()
    {
        const string yaml = """
            file_format: "1.2"
            my_vendor:
              x: 1
            """;

        using var listener = CreateListener();

        var document = Read(yaml);

        var evt = Assert.Single(Events(listener, UndefinedRootPropertyEventId));
        Assert.Equal(EventLevel.Informational, evt.Level);
        Assert.Equal("my_vendor", evt.Payload![0]);

        Assert.True(document.Properties.GetMapping("my_vendor").TryGetValue(out var vendor), "Expected 'my_vendor' mapping to contain a value.");
        Assert.True(vendor.GetInt("x").TryGetValue(out var x), "Expected 'x' to be present in the 'my_vendor' mapping.");
        Assert.Equal(1, x);
    }

    [Fact]
    public void Read_EmptyRootKey_IsReportedWithAReadableName()
    {
        const string yaml = """
            file_format: "1.2"
            "": 1
            """;

        using var listener = CreateListener();

        _ = Read(yaml);

        var evt = Assert.Single(Events(listener, UndefinedRootPropertyEventId));
        Assert.Equal("<empty>", evt.Payload![0]);
    }

    [Fact]
    public void Read_ExtensionKeysWhereTheSchemaPermitsThem_LoadWithoutEvents()
    {
        const string yaml = """
            file_format: "1.2"
            tracer_provider:
              sampler:
                my_sampler:
                  x: 1
            distribution:
              vendor:
                anything: 1
            instrumentation/development:
              dotnet:
                lib:
                  anything: 1
            """;

        using var listener = CreateListener();

        _ = Read(yaml);

        Assert.DoesNotContain(
            listener.CurrentMessages,
            e => e.EventId is UndefinedPropertyEventId or UndefinedRootPropertyEventId or UndefinedPropertyRetainedEventId);
    }

    [Theory]
    [InlineData("tracer_provider: [1, 2]")]
    [InlineData("tracer_provider:")]
    [InlineData("tracer_provider: 5")]
    [InlineData("log_level: { a: 1 }")]
    [InlineData("propagator: [a, b]")]
    public void Read_ValueWithTheWrongShape_StopsDescentWithoutAnError(string section)
    {
        using var listener = CreateListener();

        var document = Read($"file_format: \"1.2\"\n{section}\n");

        Assert.NotEmpty(document.Properties.Keys);
        Assert.DoesNotContain(
            listener.CurrentMessages,
            e => e.EventId is UndefinedPropertyEventId or UndefinedRootPropertyEventId or UndefinedPropertyRetainedEventId);
    }

    [Fact]
    public void Read_SharedMappingAliasedManyTimes_ReportsAnUndefinedKeyOnce()
    {
        const int copies = 1_000;
        var yaml = new StringBuilder()
            .AppendLine("file_format: \"1.2\"")
            .AppendLine("tracer_provider:")
            .AppendLine("  processors:")
            .AppendLine("    - batch: &shared")
            .AppendLine("        exporter:")
            .AppendLine("          otlp_http:")
            .AppendLine("            endpont: x");

        for (var i = 1; i < copies; i++)
        {
            _ = yaml.AppendLine("    - batch: *shared");
        }

        using var listener = CreateListener();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => Read(yaml.ToString()));

        Assert.DoesNotContain("more undefined", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            ["tracer_provider.processors[0].batch.exporter.otlp_http.endpont@7:13"],
            UndefinedKeys(listener));
    }

    [Fact]
    public void Read_NewerFileFormat_RetainsUndefinedKeysAndWarns()
    {
        const string yaml = """
            file_format: "1.3"
            resource:
              foo: 1
            """;

        using var listener = CreateListener();

        var document = Read(yaml);

        _ = Assert.Single(listener.CurrentMessages, e => e.EventId == FileFormatWarningEventId);
        AssertRetained(listener, "resource.foo", "1.3", SchemaPropertyNameValidator.NewerFileFormatReason);
        Assert.True(document.Properties.GetMapping("resource").TryGetValue(out var resource), "Expected 'resource' mapping to contain a value.");
        Assert.Contains("foo", resource.Keys);
    }

    [Fact]
    public void Read_FileFormatAtTheSchemaMinor_DoesNotWarnAboutTheVersion()
    {
        using var listener = CreateListener();

        _ = Read("file_format: \"1.2\"\n");

        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == FileFormatWarningEventId);
    }

    [Theory]
    [InlineData(
        "file_format: \"1.2\"\ninstrumentation/development:\n  general:\n    foo: 1\n",
        "instrumentation/development.general.foo")]
    [InlineData(
        "file_format: \"1.2\"\nresource:\n  detection/development:\n    foo: 1\n",
        "resource.detection/development.foo")]
    [InlineData(
        "file_format: \"1.2\"\nresource:\n  detection/development:\n    detectors:\n      - host:\n          foo: 1\n",
        "resource.detection/development.detectors[0].host.foo")]
    [InlineData(
        "file_format: \"1.2\"\nresource:\n  future/development:\n    x: 1\n",
        "resource.future/development")]
    public void Read_UndefinedKeyInOrUnderAnExperimentalProperty_IsRetainedAndWarns(string yaml, string path)
    {
        using var listener = CreateListener();

        _ = Read(yaml);

        AssertRetained(listener, path, "1.2", SchemaPropertyNameValidator.ExperimentalReason);
        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == UndefinedPropertyEventId);
    }

    [Theory]
    [InlineData("""
        file_format: "1.2"
        tracer_provider:
          sampler:
            jaeger_remote/development:
              initial_sampler: &shared
                parent_based:
                  typo: true
            parent_based:
              root: *shared
        """)]
    [InlineData("""
        file_format: "1.2"
        tracer_provider:
          sampler:
            parent_based:
              root: &shared
                parent_based:
                  typo: true
            jaeger_remote/development:
              initial_sampler: *shared
        """)]
    public void Read_SamplerSharedFromExperimentalAndStablePaths_FailsOnTheStablePathWhicheverIsVisitedFirst(string yaml)
    {
        const string path = "tracer_provider.sampler.parent_based.root.parent_based.typo";
        using var listener = CreateListener();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => Read(yaml));

        Assert.StartsWith($"Property '{path}'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(path, Assert.Single(Events(listener, UndefinedPropertyEventId)).Payload![0]);
    }

    [Fact]
    public void Read_SamplerSharedOnlyThroughAnExperimentalPath_IsRetainedAndWarnsOnce()
    {
        const string yaml = """
            file_format: "1.2"
            tracer_provider:
              sampler:
                jaeger_remote/development:
                  initial_sampler:
                    parent_based:
                      typo: true
            """;

        using var listener = CreateListener();

        _ = Read(yaml);

        AssertRetained(
            listener,
            "tracer_provider.sampler.jaeger_remote/development.initial_sampler.parent_based.typo",
            "1.2",
            SchemaPropertyNameValidator.ExperimentalReason);
    }

    private static TestEventListener CreateListener() =>
        new(OpenTelemetryDeclarativeConfigurationEventSource.Log, EventLevel.Verbose);

    private static DeclarativeConfigurationDocument Read(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)));
    }

    private static string[] Entries(string? expected) =>
        (expected ?? throw new ArgumentNullException(nameof(expected))).Split(';');

    private static List<EventWrittenEventArgs> Events(TestEventListener listener, int eventId) =>
        [.. listener.CurrentMessages.Where(e => e.EventId == eventId)];

    private static List<string> UndefinedKeys(TestEventListener listener) =>
        [.. Events(listener, UndefinedPropertyEventId).Select(e => $"{e.Payload![0]}@{e.Payload[1]}:{e.Payload[2]}")];

    private static List<string[]> RetainedPayloads(TestEventListener listener) =>
        [.. Events(listener, UndefinedPropertyRetainedEventId).Select(e => e.Payload!.Select(p => (string)p!).ToArray())];

    private static void AssertRetained(TestEventListener listener, string path, string fileFormat, string reason)
    {
        var evt = Assert.Single(Events(listener, UndefinedPropertyRetainedEventId));
        Assert.Equal(EventLevel.Warning, evt.Level);
        Assert.Equal(
            [path, fileFormat, ConfigurationSchema.Version, reason],
            evt.Payload!.Select(p => (string)p!));
    }

    private static string ExpectedMessage(string firstEntry, int others)
    {
        var separator = firstEntry.LastIndexOf('@');
        var position = firstEntry.Substring(separator + 1).Split(':');
        var message =
            $"Property '{firstEntry.Substring(0, separator)}' (line {position[0]}, column {position[1]}) is not defined by " +
            $"OpenTelemetry configuration schema {ConfigurationSchema.Version} and is not permitted at this location.";

        return others switch
        {
            0 => message,
            1 => message + " 1 more undefined property was reported through EventSource.",
            _ => message + $" {others} more undefined properties were reported through EventSource.",
        };
    }

    private static SchemaNode ObjectNode(AdditionalPropertiesKind additionalProperties, params (string Name, SchemaNode Node)[] properties) =>
        new()
        {
            ValueTypes = SchemaValueTypes.Object,
            AdditionalProperties = additionalProperties,
            Properties = properties.ToDictionary(property => property.Name, property => property.Node, StringComparer.Ordinal),
        };

    private static SchemaNode ArrayNode(SchemaNode items) =>
        new() { ValueTypes = SchemaValueTypes.Array, Items = items };

    private static ConfigProperties Mapping(string key, ConfigProperties child) =>
        new ConfigPropertiesBuilder().Add(key, child).Build();

    private static ConfigProperties Typo() =>
        new ConfigPropertiesBuilder().Add("typo", "x").Build();
}
