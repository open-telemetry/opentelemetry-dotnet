// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class SupportManifestTests
{
    [Fact]
    public void HandledPaths_ResolveInTheSchema()
    {
        var unresolved = SupportManifest.HandledPaths.Where(path => ResolveSchemaPath(path) is null).ToList();

        Assert.Empty(unresolved);
    }

    [Fact]
    public void HandledPaths_AreNotNested()
    {
        var nested = SupportManifest.HandledPaths
            .Where(path => SupportManifest.HandledPaths.Any(other =>
                path.StartsWith(other + ".", StringComparison.Ordinal) || path.StartsWith(other + "[].", StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(nested);
    }

    [Fact]
    public void EvaluateUnapplied_PresentNull_IsReported()
    {
        var unapplied = Assert.Single(Evaluate("""
            file_format: "1.2"
            meter_provider:
            """));

        Assert.Equal(new("meter_provider", SchemaStability.Stable, new(2, 1)), unapplied);
    }

    [Fact]
    public void EvaluateUnapplied_Mapping_IsReported()
    {
        var unapplied = Assert.Single(Evaluate("""
            file_format: "1.2"
            attribute_limits:
              attribute_count_limit: 128
            """));

        Assert.Equal(new("attribute_limits", SchemaStability.Stable, new(2, 1)), unapplied);
    }

    [Fact]
    public void EvaluateUnapplied_CoveredPaths_AreNotReported()
    {
        var unapplied = Evaluate("""
            file_format: "1.2"
            disabled: false
            resource:
              attributes:
                - name: service.name
                  value: test
              attributes_list: "a=b"
              schema_url: https://example.com/schema
            instrumentation/development:
              dotnet:
                lib:
                  setting: 1
            distribution:
              vendor:
                setting: 1
            """);

        Assert.Empty(unapplied);
    }

    [Fact]
    public void EvaluateUnapplied_UndefinedProperty_IsNotReported()
    {
        var unapplied = Evaluate("""
            file_format: "1.2"
            x_vendor_extension: 1
            resource:
              vendor_setting/development: 1
            """);

        Assert.Empty(unapplied);
    }

    [Fact]
    public void EvaluateUnapplied_ExperimentalProperty_IsReportedOnceWithoutItsChildren()
    {
        var unapplied = Assert.Single(Evaluate("""
            file_format: "1.2"
            resource:
              detection/development:
                detectors:
                  - host:
            """));

        Assert.Equal(new("resource.detection/development", SchemaStability.Experimental, new(3, 3)), unapplied);
    }

    [Fact]
    public void EvaluateUnapplied_ChildOfExperimentalContainer_InheritsExperimentalStability()
    {
        var unapplied = Assert.Single(Evaluate(
            """
            file_format: "1.2"
            logger_provider:
              logger_configurator/development:
                default_config:
                  disabled: true
                loggers:
                  - name: a
                    config:
                      disabled: true
            """,
            "logger_provider.logger_configurator/development.default_config"));

        Assert.Equal("logger_provider.logger_configurator/development.loggers[0]", unapplied.Path);
        Assert.Equal(SchemaStability.Experimental, unapplied.Stability);
    }

    [Fact]
    public void EvaluateUnapplied_ContainerWithACoveredChild_ReportsTheOutermostUnappliedChildren()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            logger_provider:
              processors:
                - simple:
                    exporter:
                      console:
              limits:
                attribute_count_limit: 10
              logger_configurator/development:
                default_config:
            """,
            "logger_provider.logger_configurator/development");

        Assert.Equal(["logger_provider.processors[0]", "logger_provider.limits"], unapplied.Select(p => p.Path));
    }

    [Fact]
    public void EvaluateUnapplied_NonEmptySequence_IsReportedPerItemWithEachItemPosition()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            logger_provider:
              processors:
                - simple:
                    exporter:
                      console:
                - batch:
                    exporter:
                      console:
              logger_configurator/development:
                default_config:
            """,
            "logger_provider.logger_configurator/development");

        Assert.Equal(["logger_provider.processors[0]", "logger_provider.processors[1]"], unapplied.Select(p => p.Path));
        Assert.Equal([4, 7], unapplied.Select(p => p.Position.Line));
    }

    [Fact]
    public void EvaluateUnapplied_EmptySequence_IsReportedOnceAsAWhole()
    {
        var unapplied = Assert.Single(Evaluate(
            """
            file_format: "1.2"
            logger_provider:
              processors: []
              logger_configurator/development:
                default_config:
            """,
            "logger_provider.logger_configurator/development"));

        Assert.Equal("logger_provider.processors", unapplied.Path);
    }

    [Fact]
    public void EvaluateUnapplied_NullInsideContainer_IsReported()
    {
        var unapplied = Assert.Single(Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
              limits:
              sampler:
                always_on:
            """,
            "tracer_provider.sampler"));

        Assert.Equal("tracer_provider.limits", unapplied.Path);
    }

    [Fact]
    public void EvaluateUnapplied_CoveredPathBelowASequence_MatchesEachItem()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            logger_provider:
              processors:
                - simple:
                    exporter:
                      console:
                - batch:
                    exporter:
                      console:
            """,
            "logger_provider.processors[].simple");

        Assert.Equal(["logger_provider.processors[1].batch"], unapplied.Select(p => p.Path));
    }

    [Fact]
    public void EvaluateUnapplied_NodeSharedByAnchor_IsReportedOnce()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
              limits: &limits
                attribute_count_limit: 10
            attribute_limits: *limits
            """,
            "tracer_provider.processors");

        Assert.Equal(["tracer_provider.limits"], unapplied.Select(p => p.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvaluateUnapplied_NodeSharedAcrossStabilities_IsReportedOncePerStability(bool experimentalFirst)
    {
        var yaml = experimentalFirst
            ? """
            file_format: "1.2"
            resource:
              detection/development: &shared {}
            attribute_limits: *shared
            """
            : """
            file_format: "1.2"
            attribute_limits: &shared {}
            resource:
              detection/development: *shared
            """;

        var unapplied = Evaluate(yaml);

        UnappliedProperty[] expected = experimentalFirst
            ? [
                new("resource.detection/development", SchemaStability.Experimental, new(3, 3)),
                new("attribute_limits", SchemaStability.Stable, new(4, 1)),
              ]
            : [
                new("attribute_limits", SchemaStability.Stable, new(2, 1)),
                new("resource.detection/development", SchemaStability.Experimental, new(4, 3)),
              ];

        Assert.Equal(expected, unapplied);
    }

    [Fact]
    public void EvaluateUnapplied_NodeSharedByAnchor_CoveredUnderOnePathOnly_IsReportedAtTheOtherPath()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
              limits: &limits
                attribute_count_limit: 10
            attribute_limits: *limits
            """,
            "tracer_provider.limits");

        Assert.Equal(["attribute_limits"], unapplied.Select(p => p.Path));
    }

    [Fact]
    public void EvaluateUnapplied_NodeSharedByAnchorAtTheSameManifestPath_IsVisitedOnce()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            logger_provider:
              processors:
                - &processor
                  batch:
                    exporter:
                      console:
                  simple:
                    exporter:
                      console:
                - *processor
            """,
            "logger_provider.processors[].simple");

        Assert.Equal(["logger_provider.processors[0].batch"], unapplied.Select(p => p.Path));
    }

    private static IReadOnlyList<UnappliedProperty> Evaluate(string yaml, params string[] additionalCoveredPaths)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var properties = DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)), _ => null).Properties;
        return SupportManifest.EvaluateUnapplied(
            properties,
            ConfigurationSchema.Pinned.Root,
            [.. SupportManifest.HandledPaths, .. additionalCoveredPaths]);
    }

    private static SchemaNode? ResolveSchemaPath(string path)
    {
        SchemaNode? node = ConfigurationSchema.Pinned.Root;
        foreach (var segment in path.Split('.'))
        {
            var isSequence = segment.EndsWith("[]", StringComparison.Ordinal);
            if (node is null || !node.Properties.TryGetValue(isSequence ? segment.Substring(0, segment.Length - 2) : segment, out node))
            {
                return null;
            }

            node = isSequence ? node.Items : node;
        }

        return node;
    }
}
