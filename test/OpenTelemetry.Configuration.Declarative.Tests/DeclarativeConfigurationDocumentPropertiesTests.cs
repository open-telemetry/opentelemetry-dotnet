// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationDocumentPropertiesTests
{
    private const string UnsetVariable = "OTEL_DECLARATIVE_TEST_DOC_UNSET";

    public static TheoryData<string> InvalidRetainedDocuments =>
    [

        // Equivalent spellings of the same key are duplicates: a mapping cannot hold both, and
        // dropping one silently loses authored data.
        """
        file_format: "1.0"
        vendor:
          key: first
          !!str key: second
        """,

        // Property names are strings; an integer key has no place in the data model.
        """
        file_format: "1.0"
        vendor:
          !!int 42: value
        """,

        // An explicit tag that contradicts the node kind: a !!str mapping is not a mapping.
        """
        file_format: "1.0"
        vendor: !!str
          key: value
        """,

        // A scalar whose value is invalid for its explicit tag.
        """
        file_format: "1.0"
        vendor:
          count: !!int abc
        """,
    ];

    [Fact]
    public void Properties_ContainsInterpretedAndUninterpretedRootSections()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            disabled: true
            resource:
              attributes:
                - name: service.name
                  value: my-service
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      otlp_http:
                        endpoint: http://localhost:4318
            propagator:
              composite: [tracecontext, baggage]
            """);

        Assert.Equal(
            ["disabled", "file_format", "propagator", "resource", "tracer_provider"],
            properties.Keys.OrderBy(k => k, StringComparer.Ordinal));

        Assert.Equal("1.0", AssertPresent(properties.GetString("file_format")));
        Assert.True(AssertPresent(properties.GetBoolean("disabled")), "The 'disabled' property should be true.");
        Assert.NotNull(AssertPresent(properties.GetProperties("resource")));
    }

    [Fact]
    public void Properties_UninterpretedSection_IsReadableToNestedDepth()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      otlp_http:
                        endpoint: http://localhost:4318
                        timeout: 10000
            """);

        var processors = AssertPresent(
            AssertPresent(properties.GetProperties("tracer_provider")).GetPropertiesList("processors"));

        var otlp = AssertPresent(
            AssertPresent(
                AssertPresent(Assert.Single(processors).GetProperties("batch")).GetProperties("exporter"))
            .GetProperties("otlp_http"));

        Assert.Equal("http://localhost:4318", AssertPresent(otlp.GetString("endpoint")));
        Assert.Equal(10000, AssertPresent(otlp.GetInt("timeout")));
    }

    [Fact]
    public void Properties_ScalarSequence_IsReadableAsScalarList()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            propagator:
              composite: [tracecontext, baggage]
              weights: [1, 2, 3]
            """);

        var propagator = AssertPresent(properties.GetProperties("propagator"));

        Assert.Equal(["tracecontext", "baggage"], AssertPresent(propagator.GetScalarList<string>("composite")));
        Assert.Equal([1L, 2L, 3L], AssertPresent(propagator.GetScalarList<long>("weights")));
    }

    // The specification's worked example: absent, present-null and present must stay distinguishable
    // all the way through a parsed document, including for a mapping-valued key.
    [Fact]
    public void Properties_ThreeStates_SurviveEndToEnd()
    {
        var vendor = AssertPresent(ReadProperties("""
            file_format: "1.0"
            vendor:
              present: value
              explicit_null: null
              tilde_null: ~
              aggregation:
                drop:
            """).GetProperties("vendor"));

        Assert.Equal(ConfigValueOutcome.Absent, vendor.GetString("missing").Outcome);
        Assert.Equal(ConfigValueOutcome.PresentNull, vendor.GetString("explicit_null").Outcome);
        Assert.Equal(ConfigValueOutcome.PresentNull, vendor.GetString("tilde_null").Outcome);
        Assert.Equal("value", AssertPresent(vendor.GetString("present")));

        var aggregation = AssertPresent(vendor.GetProperties("aggregation"));
        Assert.Equal(ConfigValueOutcome.PresentNull, aggregation.GetProperties("drop").Outcome);
    }

    [Fact]
    public void Properties_UntypableSequences_AreRetainedAndMismatchOnRead()
    {
        var vendor = AssertPresent(ReadProperties("""
            file_format: "1.0"
            vendor:
              mixed: [1, two, true]
              nested: [[1, 2], [3]]
            """).GetProperties("vendor"));

        Assert.Equal(["mixed", "nested"], vendor.Keys.OrderBy(k => k, StringComparer.Ordinal));

        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetScalarList<string>("mixed").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetScalarList<long>("mixed").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetPropertiesList("mixed").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetScalarList<long>("nested").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetPropertiesList("nested").Outcome);
    }

    // A value whose kind is known but whose CLR representation is unavailable loads: an unquoted
    // 21-digit identifier in a section nobody reads must not take down startup.
    [Fact]
    public void Properties_UnrepresentableNumbers_LoadAndMismatchOnNumericRead()
    {
        var vendor = AssertPresent(ReadProperties("""
            file_format: "1.0"
            vendor:
              huge: 123456789012345678901
              overflow: 1e999
              underflow: 1e-999
            """).GetProperties("vendor"));

        Assert.Equal(["huge", "overflow", "underflow"], vendor.Keys.OrderBy(k => k, StringComparer.Ordinal));

        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetLong("huge").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetInt("huge").Outcome);
        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetDouble("huge").Outcome);

        // Float overflow normalises to a signed infinity, which is already in the value space.
        Assert.Equal(double.PositiveInfinity, AssertPresent(vendor.GetDouble("overflow")));
        Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetLong("overflow").Outcome);
        Assert.Equal(0.0, AssertPresent(vendor.GetDouble("underflow")));
    }

    [Fact]
    public void Properties_UninterpretedSection_IsSubstituted()
    {
        var vendor = AssertPresent(ReadProperties(
            """
            file_format: "1.0"
            vendor:
              set: ${SET_VAR}
              quoted_unset: "${UNSET_VAR}"
              unset_with_default: ${UNSET_VAR:-fallback}
              empty_with_default: ${EMPTY_VAR:-fallback}
              escaped: $${NOT_A_REFERENCE}
            """,
            name => name switch
            {
                "EMPTY_VAR" => string.Empty,
                "SET_VAR" => "resolved-value",
                _ => null,
            }).GetProperties("vendor"));

        Assert.Equal("resolved-value", AssertPresent(vendor.GetString("set")));
        Assert.Equal("fallback", AssertPresent(vendor.GetString("unset_with_default")));
        Assert.Equal("fallback", AssertPresent(vendor.GetString("empty_with_default")));
        Assert.Equal("${NOT_A_REFERENCE}", AssertPresent(vendor.GetString("escaped")));

        // Quoting suppresses YAML null inference, so the empty substitution result stays a string.
        Assert.Equal(string.Empty, AssertPresent(vendor.GetString("quoted_unset")));
    }

    // An unset variable with no default substitutes to the empty string, and an empty plain scalar
    // is YAML null under the core schema, which is what the read reports.
    [Fact]
    public void Properties_UnsetVariableWithoutDefault_IsNullAndReported()
    {
        using var environment = EnvironmentVariableScope.Create(UnsetVariable, null);
        using var listener = CreateVerboseListener();

        var vendor = AssertPresent(ReadProperties($$"""
            file_format: "1.0"
            vendor:
              setting: ${{{UnsetVariable}}}
            """).GetProperties("vendor"));

        Assert.Equal(ConfigValueOutcome.PresentNull, vendor.GetString("setting").Outcome);

        var notSet = Assert.Single(listener.Messages, e => e.EventId == 15);
        Assert.Equal(UnsetVariable, notSet.Payload![0]);
    }

    // The other two substitution diagnostics newly reach retained content. An empty variable with no
    // default is reported separately from an unset one, because the spec distinguishes them.
    [Fact]
    public void Properties_EmptyVariableWithoutDefault_IsEmptyAndReported()
    {
        using var listener = CreateVerboseListener();

        var vendor = AssertPresent(ReadProperties(
            $$"""
            file_format: "1.0"
            vendor:
              setting: "${{{UnsetVariable}}}"
            """,
            name => name == UnsetVariable ? string.Empty : null).GetProperties("vendor"));

        Assert.Equal(string.Empty, AssertPresent(vendor.GetString("setting")));

        var empty = Assert.Single(listener.Messages, e => e.EventId == 16);
        Assert.Equal(UnsetVariable, empty.Payload![0]);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == 15);
    }

    // An unclosed `${` cannot form a complete reference, so it is emitted literally rather than
    // failing the parse - but the user is told, and the text survives into the retained tree.
    [Fact]
    public void Properties_UnclosedReferenceInUninterpretedSection_IsRetainedLiterallyAndReported()
    {
        using var listener = CreateVerboseListener();

        var vendor = AssertPresent(ReadProperties("""
            file_format: "1.0"
            vendor:
              setting: "${UNCLOSED"
            """).GetProperties("vendor"));

        Assert.Equal("${UNCLOSED", AssertPresent(vendor.GetString("setting")));

        var unresolved = Assert.Single(listener.Messages, e => e.EventId == 24);
        Assert.Equal("${UNCLOSED", unresolved.Payload![0]);
    }

    [Fact]
    public void Properties_MergeKeyInUninterpretedSection_FailsToLoad()
    {
        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadProperties("""
            file_format: "1.0"
            base: &base
              shared: from-base
            vendor:
              <<: *base
            """));

        Assert.Contains("<root>.vendor.<<", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Properties_QuotedMergeLikeKeyInUninterpretedSection_IsRetained()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            vendor:
              "<<": value
            """);

        var vendor = AssertPresent(properties.GetProperties("vendor"));
        Assert.Equal("value", AssertPresent(vendor.GetString("<<")));
    }

    [Fact]
    public void Properties_InvalidReferenceInUninterpretedSection_FailsTheWholeParse()
    {
        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadProperties("""
            file_format: "1.0"
            disabled: true
            vendor:
              setting: ${MY.VAR}
            """));

        Assert.Contains("${MY.VAR}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ResolvesEveryReferenceExactlyOncePerParse()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        _ = ReadProperties(
            """
            file_format: "1.0"
            disabled: ${DISABLED_VAR}
            resource:
              attributes_list: ${ATTRIBUTES_VAR}
            anchored: &shared
              endpoint: ${ENDPOINT_VAR}
            first_alias: *shared
            second_alias: *shared
            """,
            name =>
            {
                counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
                return name switch
                {
                    "ATTRIBUTES_VAR" => "service.name=my-service",
                    "DISABLED_VAR" => "true",
                    "ENDPOINT_VAR" => "http://localhost:4318",
                    _ => null,
                };
            });

        Assert.Equal(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["ATTRIBUTES_VAR"] = 1,
                ["DISABLED_VAR"] = 1,
                ["ENDPOINT_VAR"] = 1,
            },
            counts);
    }

    [Fact]
    public void Read_UnsetVariableInAnchoredNode_IsReportedOncePerParse()
    {
        using var environment = EnvironmentVariableScope.Create(UnsetVariable, null);
        using var listener = CreateVerboseListener();

        _ = ReadProperties($$"""
            file_format: "1.0"
            anchored: &shared
              setting: ${{{UnsetVariable}}}
            first_alias: *shared
            second_alias: *shared
            """);

        Assert.Single(listener.Messages, e => e.EventId == 15);
    }

    // An alias is the same node object, so every occurrence shares one immutable value - and, as a
    // direct consequence, reports the position at which the anchor was authored.
    [Fact]
    public void Properties_AcyclicAlias_IsSharedByEveryOccurrence()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            anchored: &shared
              endpoint: http://localhost:4318
            first_alias: *shared
            second_alias: *shared
            """);

        foreach (var key in new[] { "anchored", "first_alias", "second_alias" })
        {
            Assert.Equal(
                "http://localhost:4318",
                AssertPresent(AssertPresent(properties.GetProperties(key)).GetString("endpoint")));
        }

        var anchor = properties.GetProperties("anchored").Position;
        Assert.True(anchor.HasPosition);
        Assert.Equal(anchor.Line, properties.GetProperties("first_alias").Position.Line);
        Assert.Equal(anchor.Column, properties.GetProperties("second_alias").Position.Column);
    }

    [Fact]
    public void Properties_MutuallyReferentialAliases_FailToLoad()
    {
        var exception = Assert.Throws<DeclarativeConfigurationException>(() => ReadProperties("""
            file_format: "1.0"
            first: &first
              next: &second
                back: *first
            """));

        Assert.Contains("<root>.first.next.back", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Properties_AliasCycleInSequence_FailsToLoad() =>
        Assert.Throws<DeclarativeConfigurationException>(() => ReadProperties("""
            file_format: "1.0"
            items: &items
              - *items
            """));

    // The same scalar node can appear as a key and as a value. Key resolution never substitutes and
    // value resolution always does, so the two paths must not be conflated.
    [Fact]
    public void Properties_AnchoredScalarUsedAsKeyAndValue_IsReadCorrectly()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            &anchor vendor_key: some-value
            reference: *anchor
            """);

        Assert.Equal("some-value", AssertPresent(properties.GetString("vendor_key")));
        Assert.Equal("vendor_key", AssertPresent(properties.GetString("reference")));
    }

    [Theory]
    [MemberData(nameof(InvalidRetainedDocuments))]
    public void Read_InvalidContentInUninterpretedSection_FailsTheParse(string yaml) =>
        Assert.Throws<DeclarativeConfigurationException>(() => ReadProperties(yaml));

    [Fact]
    public void Read_IdenticalDuplicateKeys_FailAtLoad() =>
        Assert.Throws<YamlDotNet.Core.YamlException>(() => ReadProperties("""
            file_format: "1.0"
            vendor:
              key: first
              key: second
            """));

    [Fact]
    public void Properties_CaptureTheAuthoredSourcePosition()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            vendor:
              scalar: some-value
              items:
                - first
            """);

        Assert.Equal(1, properties.GetString("file_format").Position.Line);

        var vendorResult = properties.GetProperties("vendor");
        Assert.Equal(3, vendorResult.Position.Line);
        Assert.Equal(3, vendorResult.Position.Column);
        var vendor = AssertPresent(vendorResult);

        var scalar = vendor.GetString("scalar");
        Assert.Equal(3, scalar.Position.Line);
        Assert.Equal(11, scalar.Position.Column);

        var items = vendor.GetScalarList<string>("items");
        Assert.Equal(5, items.Position.Line);
        Assert.Equal(5, items.Position.Column);
    }

    [Fact]
    public void Position_BuilderConstructedValue_IsUnknown()
    {
        var properties = new ConfigPropertiesBuilder()
            .Add("key", ConfigValue.String("value"))
            .Build();

        Assert.False(properties.GetString("key").Position.HasPosition, "The position of a builder-constructed value should not have a position.");
    }

    [Fact]
    public void WithPosition_PreservesKindAndPayload()
    {
        var positioned = ConfigValue.String("value").WithPosition(new(3, 5));

        Assert.Equal(ConfigValueKind.String, positioned.Kind);
        Assert.Equal("value", positioned.AsString());
        Assert.Equal(3, positioned.Position.Line);
        Assert.Equal(5, positioned.Position.Column);
    }

    [Fact]
    public void WithPosition_AppliesToNull()
    {
        var positioned = ConfigValue.Null.WithPosition(new(7, 2));

        Assert.Equal(ConfigValueKind.Null, positioned.Kind);
        Assert.Equal(7, positioned.Position.Line);
        Assert.False(ConfigValue.Null.Position.HasPosition, "The shared null default should not have a position.");
    }

    [Fact]
    public void Properties_EmptyFile_IsEmpty()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var document = DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(string.Empty)));

        Assert.Same(ConfigProperties.Empty, document.Properties);
    }

    [Fact]
    public void Properties_MultipleDocumentStream_ConvertsTheFirstDocumentOnly()
    {
        var properties = ReadProperties("""
            file_format: "1.0"
            first_only: yes-it-is
            ---
            file_format: "1.0"
            second_document: should-not-appear
            """);

        Assert.Equal(["file_format", "first_only"], properties.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void Properties_AreSharedAcrossAccessorCalls()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(factory.CreateYamlFile("""
                file_format: "1.0"
                vendor:
                  setting: value
                """)));

        Assert.Same(accessor.GetDocument().Properties, accessor.GetDocumentForProvider().Properties);
    }

    [Fact]
    public void FlatKeys_AreUnaffectedByUninterpretedSections()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var document = DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile("""
            file_format: "1.0"
            disabled: true
            resource:
              attributes:
                - name: service.name
                  value: my-service
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      otlp_http:
                        endpoint: http://localhost:4318
            propagator:
              composite: [tracecontext, baggage]
            """)));

        Assert.Equal(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [DeclarativeConfigurationConverter.DisabledKey] = "true",
                ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=my-service",
            },
            document.FlatKeys);
    }

    private static ConfigProperties ReadProperties(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml))).Properties;
    }

    private static ConfigProperties ReadProperties(string yaml, Func<string, string?> resolveVariable)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader
            .Read(new FilePath(factory.CreateYamlFile(yaml)), resolveVariable)
            .Properties;
    }

    private static T AssertPresent<T>(ConfigValueResult<T> result)
    {
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        return result.Value!;
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
}
