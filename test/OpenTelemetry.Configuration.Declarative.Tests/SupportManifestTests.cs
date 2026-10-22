// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class SupportManifestTests
{
    [Fact]
    public void CoveredPaths_ResolveInTheSchema()
    {
        var unresolved = SupportManifest.CoveredPaths.Where(path => ResolveSchemaPath(path) is null).ToList();

        Assert.Empty(unresolved);
    }

    [Fact]
    public void CoveredPaths_AreNotNested()
    {
        var nested = SupportManifest.CoveredPaths
            .Where(path => SupportManifest.CoveredPaths.Any(other =>
                path.StartsWith(other + ".", StringComparison.Ordinal) || path.StartsWith(other + "[].", StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(nested);
    }

    [Fact]
    public void EvaluateUnapplied_PresentNull_IsReported()
    {
        var unapplied = Assert.Single(Evaluate("""
            file_format: "1.2"
            tracer_provider:
            """));

        Assert.Equal(new("tracer_provider", SchemaStability.Stable, new(2, 1)), unapplied);
    }

    [Fact]
    public void EvaluateUnapplied_Scalar_IsReported()
    {
        var unapplied = Assert.Single(Evaluate("""
            file_format: "1.2"
            log_level: debug
            """));

        Assert.Equal(new("log_level", SchemaStability.Stable, new(2, 1)), unapplied);
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
            tracer_provider:
              tracer_configurator/development:
                default_config:
                  disabled: true
                tracers:
                  - name: a
                    config:
                      disabled: true
            """,
            "tracer_provider.tracer_configurator/development.default_config"));

        Assert.Equal("tracer_provider.tracer_configurator/development.tracers[0]", unapplied.Path);
        Assert.Equal(SchemaStability.Experimental, unapplied.Stability);
    }

    [Fact]
    public void EvaluateUnapplied_ContainerWithACoveredChild_ReportsTheOutermostUnappliedChildren()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
              processors:
                - simple:
                    exporter:
                      console:
              limits:
                attribute_count_limit: 10
              sampler:
                always_on:
            """,
            "tracer_provider.sampler");

        Assert.Equal(["tracer_provider.processors[0]", "tracer_provider.limits"], unapplied.Select(p => p.Path));
    }

    [Fact]
    public void EvaluateUnapplied_NonEmptySequence_IsReportedPerItemWithEachItemPosition()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
              processors:
                - simple:
                    exporter:
                      console:
                - batch:
                    exporter:
                      console:
              sampler:
                always_on:
            """,
            "tracer_provider.sampler");

        Assert.Equal(["tracer_provider.processors[0]", "tracer_provider.processors[1]"], unapplied.Select(p => p.Path));
        Assert.Equal([4, 7], unapplied.Select(p => p.Position.Line));
    }

    [Fact]
    public void EvaluateUnapplied_EmptySequence_IsReportedOnceAsAWhole()
    {
        var unapplied = Assert.Single(Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
              processors: []
              sampler:
                always_on:
            """,
            "tracer_provider.sampler"));

        Assert.Equal("tracer_provider.processors", unapplied.Path);
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
            tracer_provider:
              processors:
                - simple:
                    exporter:
                      console:
                - batch:
                    exporter:
                      console:
            """,
            "tracer_provider.processors[].simple");

        Assert.Equal(["tracer_provider.processors[1].batch"], unapplied.Select(p => p.Path));
    }

    [Fact]
    public void EvaluateUnapplied_NodeSharedByAnchor_IsReportedOnce()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
              sampler:
                parent_based:
                  root: &sampler
                    always_on:
                  remote_parent_sampled: *sampler
            """,
            "tracer_provider.sampler.parent_based.local_parent_sampled");

        Assert.Equal(["tracer_provider.sampler.parent_based.root"], unapplied.Select(p => p.Path));
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
              sampler:
                parent_based:
                  root: &sampler
                    always_on:
                  remote_parent_sampled: *sampler
            """,
            "tracer_provider.sampler.parent_based.root");

        Assert.Equal(["tracer_provider.sampler.parent_based.remote_parent_sampled"], unapplied.Select(p => p.Path));
    }

    [Fact]
    public void EvaluateUnapplied_NodeSharedByAnchorAtTheSameManifestPath_IsVisitedOnce()
    {
        var unapplied = Evaluate(
            """
            file_format: "1.2"
            tracer_provider:
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
            "tracer_provider.processors[].simple");

        Assert.Equal(["tracer_provider.processors[0].batch"], unapplied.Select(p => p.Path));
    }

    private static IReadOnlyList<UnappliedProperty> Evaluate(string yaml, params string[] additionalCoveredPaths)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var properties = DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)), _ => null).Properties;
        return SupportManifest.EvaluateUnapplied(
            properties,
            ConfigurationSchema.Pinned.Root,
            [.. SupportManifest.CoveredPaths, .. additionalCoveredPaths]);
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
