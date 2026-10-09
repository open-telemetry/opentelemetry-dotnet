// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenTelemetry.Tests;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed partial class SchemaConformanceFixtureTests
{
    private const int UndefinedPropertyEventId = 25;
    private const int UndefinedRootPropertyEventId = 42;
    private const int UndefinedPropertyRetainedEventId = 43;

    private const string UndefinedKeyName = "zz_undefined_property";
    private const string UndefinedKey = UndefinedKeyName + ": 1";
    private const string TopLevelKeyPattern = @"^([A-Za-z_][A-Za-z0-9_/.\-]*):";

    private static readonly string[] ExpectedExamples =
    [
        "otel-getting-started",
        "otel-sdk-config",
        "otel-sdk-migration-config",
    ];

#if !NET
    private static readonly Regex TopLevelKey = new(TopLevelKeyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
#endif

    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Schema", "v1.2.0");

    // Pin every snippet, but mutate only one representative of each path and outcome.
    // Paths and outcomes are independent of the schema reader and validator under test.
    private static readonly Dictionary<string, (string ParentPath, UndefinedKeyOutcome Outcome)> Expectations =
        new(StringComparer.Ordinal)
        {
            ["CardinalityLimits_kitchen_sink"] =
            ("meter_provider.readers[0].periodic.cardinality_limits", UndefinedKeyOutcome.Rejected),
            ["ConsoleExporter_logs_kitchen_sink"] =
            ("logger_provider.processors[0].simple.exporter.console", UndefinedKeyOutcome.Rejected),
            ["ConsoleExporter_traces_kitchen_sink"] =
            ("tracer_provider.processors[0].simple.exporter.console", UndefinedKeyOutcome.Rejected),
            ["ConsoleMetricExporter_traces_kitchen_sink"] =
            ("meter_provider.readers[0].periodic.exporter.console", UndefinedKeyOutcome.Rejected),
            ["ExperimentalGeneralInstrumentation_semconv_stability_opt_in"] =
            ("instrumentation/development.general", UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalInstrumentation_kitchen_sink"] =
            ("instrumentation/development", UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalLoggerConfigurator_kitchen_sink"] =
            ("logger_provider.logger_configurator/development", UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalMeterConfigurator_kitchen_sink"] =
            ("meter_provider.meter_configurator/development", UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalOtlpFileExporter_logs_file"] =
            ("logger_provider.processors[0].batch.exporter.otlp_file/development",
                UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalOtlpFileExporter_logs_stdout"] =
            ("logger_provider.processors[0].batch.exporter.otlp_file/development",
                UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalOtlpFileExporter_traces_file"] =
            ("tracer_provider.processors[0].batch.exporter.otlp_file/development",
                UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalOtlpFileExporter_traces_stdout"] =
            ("tracer_provider.processors[0].batch.exporter.otlp_file/development",
                UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalOtlpFileMetricExporter_metrics_file"] =
            ("meter_provider.readers[0].periodic.exporter.otlp_file/development",
                UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalOtlpFileMetricExporter_metrics_stdout"] =
            ("meter_provider.readers[0].periodic.exporter.otlp_file/development",
                UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalPrometheusMetricExporter_kitchen_sink"] =
            ("meter_provider.readers[0].pull.exporter.prometheus/development", UndefinedKeyOutcome.RetainedWithWarning),
            ["ExperimentalTracerConfigurator_kitchen_sink"] =
            ("tracer_provider.tracer_configurator/development", UndefinedKeyOutcome.RetainedWithWarning),
            ["IdGenerator_custom"] =
            ("tracer_provider.id_generator.my_custom_id_generator", UndefinedKeyOutcome.RetainedSilently),
            ["LogRecordLimits_kitchen_sink"] =
            ("logger_provider.limits", UndefinedKeyOutcome.Rejected),
            ["MeterProvider_composable_views"] =
            ("meter_provider", UndefinedKeyOutcome.Rejected),
            ["OtlpGrpcExporter_logs_kitchen_sink"] =
            ("logger_provider.processors[0].batch.exporter.otlp_grpc", UndefinedKeyOutcome.Rejected),
            ["OtlpGrpcExporter_traces_kitchen_sink"] =
            ("tracer_provider.processors[0].batch.exporter.otlp_grpc", UndefinedKeyOutcome.Rejected),
            ["OtlpGrpcMetricExporter_metrics_kitchen_sink"] =
            ("meter_provider.readers[0].periodic.exporter.otlp_grpc", UndefinedKeyOutcome.Rejected),
            ["OtlpHttpExporter_logs_kitchen_sink"] =
            ("logger_provider.processors[0].batch.exporter.otlp_http", UndefinedKeyOutcome.Rejected),
            ["OtlpHttpExporter_traces_kitchen_sink"] =
            ("tracer_provider.processors[0].batch.exporter.otlp_http", UndefinedKeyOutcome.Rejected),
            ["OtlpHttpMetricExporter_metrics_kitchen_sink"] =
            ("meter_provider.readers[0].periodic.exporter.otlp_http", UndefinedKeyOutcome.Rejected),
            ["OtlpHttpMetricExporter_use_base2_exponential_histogram"] =
            ("meter_provider.readers[0].periodic.exporter.otlp_http", UndefinedKeyOutcome.Rejected),
            ["Propagator_kitchen_sink"] =
            ("propagator", UndefinedKeyOutcome.Rejected),
            ["Resource_kitchen_sink"] =
            ("resource", UndefinedKeyOutcome.Rejected),
            ["Sampler_always_record_typical"] =
            ("tracer_provider.sampler.always_record", UndefinedKeyOutcome.Rejected),
            ["Sampler_composite_rule_based_drop_redis_ping"] =
            ("tracer_provider.sampler.composite/development.rule_based", UndefinedKeyOutcome.RetainedWithWarning),
            ["Sampler_parent_based_typical"] =
            ("tracer_provider.sampler.parent_based", UndefinedKeyOutcome.Rejected),
            ["Sampler_probability_kitchen_sink"] =
            ("tracer_provider.sampler.probability/development", UndefinedKeyOutcome.RetainedWithWarning),
            ["Sampler_rule_based_kitchen_sink"] =
            ("tracer_provider.sampler.composite/development.rule_based.rules[0].attribute_values",
                UndefinedKeyOutcome.RetainedWithWarning),
            ["SpanLimits_kitchen_sink"] =
            ("tracer_provider.limits", UndefinedKeyOutcome.Rejected),
            ["View_kitchen_sink"] =
            ("meter_provider.views[0]", UndefinedKeyOutcome.Rejected),
            ["View_override_default_histogram_buckets"] =
            ("meter_provider.views[0]", UndefinedKeyOutcome.Rejected),
        };

    public enum UndefinedKeyOutcome
    {
        /// <summary>The undefined key is rejected.</summary>
        Rejected,

        /// <summary>The undefined key and its value are retained with a warning.</summary>
        RetainedWithWarning,

        /// <summary>The undefined key and its value are retained without a property diagnostic.</summary>
        RetainedSilently,
    }

    public static TheoryData<string> Fixtures() => [.. EnumerateFixtures("snippets"), .. EnumerateFixtures("examples")];

    public static TheoryData<string, string, UndefinedKeyOutcome> Mutations()
    {
        var data = new TheoryData<string, string, UndefinedKeyOutcome>();
        foreach (var group in Expectations.OrderBy(row => row.Key, StringComparer.Ordinal).GroupBy(row => row.Value))
        {
            var row = group.First();
            data.Add(Path.Combine("snippets", row.Key + ".yaml"), row.Value.ParentPath, row.Value.Outcome);
        }

        return data;
    }

    // The fixture sets are pinned by name so that adding or removing a file fails with a readable
    // diff, and cannot happen without the expectations below being updated to match.
    [Fact]
    public void Snippets_MatchTheExpectationRowsExactly() =>
        Assert.Equal(
            [.. Expectations.Keys.OrderBy(name => name, StringComparer.Ordinal)],
            EnumerateFixtures("snippets").Select(Path.GetFileNameWithoutExtension).ToList());

    [Fact]
    public void Examples_MatchTheExpectedSetExactly() =>
        Assert.Equal(
            [.. ExpectedExamples.OrderBy(name => name, StringComparer.Ordinal)],
            EnumerateFixtures("examples").Select(Path.GetFileNameWithoutExtension).ToList());

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Read_Fixture_LoadsWithoutUndefinedPropertyErrors(string relativePath)
    {
        using var listener = CreateListener();
        var yaml = File.ReadAllText(Path.Combine(FixtureDirectory, relativePath));

        var document = Read(yaml);

        // Check root-key preservation; nested value behavior is covered by the document tests.
        var expectedKeys = GetTopLevelKeys(yaml);
        Assert.NotEmpty(expectedKeys);
        Assert.Equal(
            expectedKeys.OrderBy(k => k, StringComparer.Ordinal),
            document.Properties.Keys.OrderBy(k => k, StringComparer.Ordinal));
        AssertNoPropertyDiagnostics(listener);
    }

    [Fact]
    public void Read_MigrationFixture_UsesDefaultsRegardlessOfProcessEnvironment()
    {
        using var envScope = EnvironmentVariableScope.Create(
            ("OTEL_SDK_DISABLED", "yes"),
            ("OTEL_SERVICE_NAME", "123"),
            ("OTEL_RESOURCE_ATTRIBUTES", "123"));
        using var listener = CreateListener();
        var yaml = File.ReadAllText(Path.Combine(FixtureDirectory, "examples", "otel-sdk-migration-config.yaml"));

        var document = Read(yaml);

        Assert.True(document.Properties.GetBoolean("disabled").TryGetValue(out var disabled));
        Assert.False(disabled);
        var resource = GetMapping(document.Properties, "resource");
        Assert.Equal(ConfigValueOutcome.PresentNull, resource.GetString("attributes_list").Outcome);
        var attribute = GetMapping(document.Properties, "resource.attributes[0]");
        Assert.True(attribute.GetString("name").TryGetValue(out var name));
        Assert.Equal("service.name", name);
        Assert.True(attribute.GetString("value").TryGetValue(out var value));
        Assert.Equal("unknown_service", value);
        AssertNoPropertyDiagnostics(listener);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public void Read_SnippetWithAnUndefinedKeyAtTheExpectedPath_MatchesTheRecordedOutcome(
        string relativePath,
        string parentPath,
        UndefinedKeyOutcome expected)
    {
        using var listener = CreateListener();
        var (yaml, line, column) = InsertUndefinedKey(
            File.ReadAllText(Path.Combine(FixtureDirectory, relativePath)),
            parentPath);
        var path = parentPath + "." + UndefinedKeyName;

        switch (expected)
        {
            case UndefinedKeyOutcome.Rejected:
                _ = Assert.Throws<DeclarativeConfigurationException>(() => Read(yaml));
                var error = Assert.Single(listener.CurrentMessages, e => e.EventId == UndefinedPropertyEventId);
                Assert.Equal([path, (long)line, (long)column], error.Payload);
                Assert.DoesNotContain(
                    listener.CurrentMessages,
                    e => e.EventId is UndefinedRootPropertyEventId or UndefinedPropertyRetainedEventId);
                break;
            case UndefinedKeyOutcome.RetainedWithWarning:
            case UndefinedKeyOutcome.RetainedSilently:
                var document = Read(yaml);
                var parent = GetMapping(document.Properties, parentPath);
                Assert.True(
                    parent.GetInt(UndefinedKeyName).TryGetValue(out var value),
                    $"Expected '{path}' to retain its integer value.");
                Assert.Equal(1, value);

                if (expected == UndefinedKeyOutcome.RetainedWithWarning)
                {
                    var warning = Assert.Single(
                        listener.CurrentMessages,
                        e => e.EventId == UndefinedPropertyRetainedEventId);
                    Assert.Equal(EventLevel.Warning, warning.Level);
                    Assert.Equal(path, warning.Payload![0]);
                    Assert.DoesNotContain(
                        listener.CurrentMessages,
                        e => e.EventId is UndefinedPropertyEventId or UndefinedRootPropertyEventId);
                }
                else
                {
                    AssertNoPropertyDiagnostics(listener);
                }

                break;
            default:
                Assert.Fail($"Unexpected undefined-key outcome: {expected}.");
                break;
        }
    }

    [Fact]
    public void Read_SnippetWithACustomSampler_RetainsTheComponentAndItsOptionsSilently()
    {
        using var listener = CreateListener();
        var lines = File.ReadAllLines(Path.Combine(FixtureDirectory, "snippets", "Sampler_parent_based_typical.yaml"));
        var selector = Array.FindIndex(
            lines,
            line => string.Equals(line, "    parent_based:", StringComparison.Ordinal));
        Assert.True(selector >= 0, "Expected the snippet to select the parent_based sampler.");
        lines[selector] = "    " + UndefinedKeyName + ":";

        var document = Read(string.Join("\n", lines));

        var sampler = GetMapping(document.Properties, "tracer_provider.sampler");
        Assert.Equal([UndefinedKeyName], sampler.Keys);
        var options = GetMapping(sampler, UndefinedKeyName + ".root.trace_id_ratio_based");
        Assert.True(options.GetDouble("ratio").TryGetValue(out var ratio));
        Assert.Equal(0.01, ratio);
        AssertNoPropertyDiagnostics(listener);
    }

    private static List<string> EnumerateFixtures(string directory) =>
        [.. Directory.GetFiles(Path.Combine(FixtureDirectory, directory), "*.yaml")
            .Select(path => Path.Combine(directory, Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.Ordinal)];

#if NET
    [GeneratedRegex(TopLevelKeyPattern, RegexOptions.CultureInvariant)]
    private static partial Regex GetTopLevelKeyRegex();
#else
    private static Regex GetTopLevelKeyRegex() => TopLevelKey;
#endif

    private static List<string> GetTopLevelKeys(string yaml) =>
        [.. yaml.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => GetTopLevelKeyRegex().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)];

    // Locate the authored mapping, then edit only the text so diagnostic positions remain meaningful.
    private static (string Yaml, int Line, int Column) InsertUndefinedKey(string snippet, string parentPath)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(snippet);
        stream.Load(reader);
        var node = Assert.Single(stream.Documents).RootNode;
        YamlScalarNode? key = null;
        foreach (var segment in SplitPath(parentPath))
        {
            if (node is YamlSequenceNode sequence)
            {
                node = sequence.Children[int.Parse(segment, CultureInfo.InvariantCulture)];
                key = null;
            }
            else
            {
                var mapping = Assert.IsType<YamlMappingNode>(node);
                var entry = Assert.Single(
                    mapping.Children,
                    entry => entry.Key is YamlScalarNode scalar
                        && string.Equals(scalar.Value, segment, StringComparison.Ordinal));
                key = Assert.IsType<YamlScalarNode>(entry.Key);
                node = entry.Value;
            }
        }

        int line;
        int indent;
        if (node is YamlMappingNode target)
        {
            Assert.NotEmpty(target.Children);
            var firstKey = target.Children.First().Key;
            line = checked((int)firstKey.Start.Line) - 1;
            indent = checked((int)firstKey.Start.Column) - 1;
        }
        else
        {
            Assert.True(
                node is YamlScalarNode { Value: null or "" },
                $"Expected '{parentPath}' to be a block mapping or an empty value.");
            Assert.NotNull(key);
            line = checked((int)key.Start.Line);
            indent = checked((int)key.Start.Column) + 1;
        }

        var lines = snippet.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        Assert.True(
            lines[line].Take(indent).All(char.IsWhiteSpace),
            $"Expected '{parentPath}' to use block mapping syntax.");
        lines.Insert(line, new string(' ', indent) + UndefinedKey);
        return (string.Join("\n", lines), line + 1, indent + 1);
    }

    private static ConfigProperties GetMapping(ConfigProperties properties, string path)
    {
        var value = ConfigValue.Mapping(properties);
        foreach (var segment in SplitPath(path))
        {
            if (value.Kind == ConfigValueKind.Sequence)
            {
                value = value.AsSequence()[int.Parse(segment, CultureInfo.InvariantCulture)];
            }
            else
            {
                Assert.Equal(ConfigValueKind.Mapping, value.Kind);
                Assert.True(
                    value.AsMapping().TryGetValue(segment, out var child),
                    $"Expected '{path}' to contain '{segment}'.");
                value = child;
            }
        }

        Assert.Equal(ConfigValueKind.Mapping, value.Kind);
        return value.AsMapping();
    }

    private static string[] SplitPath(string path) =>
        path.Split(['.', '[', ']'], StringSplitOptions.RemoveEmptyEntries);

    private static TestEventListener CreateListener() =>
        new(OpenTelemetryDeclarativeConfigurationEventSource.Log, EventLevel.Verbose);

    private static void AssertNoPropertyDiagnostics(TestEventListener listener) =>
        Assert.DoesNotContain(
            listener.CurrentMessages,
            e => e.EventId is UndefinedPropertyEventId
                or UndefinedRootPropertyEventId
                or UndefinedPropertyRetainedEventId);

    private static DeclarativeConfigurationDocument Read(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)), _ => null);
    }
}
