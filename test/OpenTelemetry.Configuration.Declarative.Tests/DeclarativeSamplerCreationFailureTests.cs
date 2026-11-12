// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeSamplerCreationFailureTests
{
    // Each row is a scenario name, a sampler written as one line of flow-style YAML, the start of
    // the message, and the fragment of the message the user must see. Every sampler names its own
    // node, so the path is the same wherever the sampler sits.
    public static IEnumerable<TheoryDataRow<string, string, string>> InvalidSamplers =>
    [
        Invalid("always_record without root", "{always_record: {}}", "'always_record' requires", "'root'"),
        Invalid("always_record with null value", "{always_record: }", "'always_record' requires", "'root'"),
        Invalid("always_record with null root", "{always_record: {root: }}", "'always_record.root' (line", "must name exactly one sampler"),
        Invalid("parent_based with null child", "{parent_based: {local_parent_sampled: }}", "'parent_based.local_parent_sampled' (line", "must name exactly one sampler"),
        Invalid("parent_based with scalar child", "{parent_based: {root: 5}}", "'parent_based.root' (line", "must name exactly one sampler"),
        Invalid("parent_based with two-key child", "{parent_based: {root: {always_on: , always_off: }}}", "'parent_based.root' (line", "must name exactly one sampler"),
        Invalid("parent_based with unsupported child", "{parent_based: {remote_parent_sampled: {x_unknown: }}}", "'parent_based.remote_parent_sampled.x_unknown' (line", "is a sampler"),
        Invalid("ratio text", "{trace_id_ratio_based: {ratio: high}}", "'trace_id_ratio_based.ratio' (line", "must be a number from 0 to 1"),
        Invalid("ratio boolean", "{trace_id_ratio_based: {ratio: true}}", "'trace_id_ratio_based.ratio' (line", "must be a number from 0 to 1"),
        Invalid("ratio below 0", "{trace_id_ratio_based: {ratio: -0.1}}", "'trace_id_ratio_based.ratio' (line", "must be a number from 0 to 1"),
        Invalid("ratio above 1", "{trace_id_ratio_based: {ratio: 1.5}}", "'trace_id_ratio_based.ratio' (line", "must be a number from 0 to 1"),
        Invalid("ratio NaN", "{trace_id_ratio_based: {ratio: .nan}}", "'trace_id_ratio_based.ratio' (line", "must be a number from 0 to 1"),
        Invalid("ratio infinity", "{trace_id_ratio_based: {ratio: .inf}}", "'trace_id_ratio_based.ratio' (line", "must be a number from 0 to 1"),
        Invalid("ratio integer above 1", "{trace_id_ratio_based: {ratio: 2}}", "'trace_id_ratio_based.ratio' (line", "must be a number from 0 to 1"),
    ];

    [Theory]
    [MemberData(nameof(InvalidSamplers))]
    public void InvalidSampler_AtTheRoot_FailsAtProviderBuild(string sampler, string start, string expected)
    {
#if NET
        ArgumentNullException.ThrowIfNull(sampler);
#else
        if (sampler is null)
        {
            throw new ArgumentNullException(nameof(sampler));
        }
#endif

        AssertFailure(sampler, start, expected);
    }

    [Theory]
    [MemberData(nameof(InvalidSamplers))]
    public void InvalidSampler_TwoLevelsDeep_FailsAtProviderBuild(string sampler, string start, string expected) =>
        AssertFailure(
            $"""
            parent_based:
              root:
                parent_based:
                  root: {sampler}
            """,
            start,
            expected);

    private static TheoryDataRow<string, string, string> Invalid(string scenario, string sampler, string start, string expected) =>
        new(sampler, start, expected) { TestDisplayName = scenario };

    private static void AssertFailure(string sampler, string start, string expected)
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(
            $$$"""
            file_format: "1.2"
            tracer_provider:
              processors:
                - simple: {exporter: {console: }}
              sampler:
                {{{string.Join("\n    ", sampler.Split('\n'))}}}

            """);
        var services = new ServiceCollection();
        services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<DeclarativeConfigurationException>(provider.GetRequiredService<TracerProvider>);

        Assert.Contains(start, exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }
}
