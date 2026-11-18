// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeSamplerBindingTests
{
    // Each row is a scenario name, a sampler written as one line of flow-style YAML, the path of the
    // offending node relative to 'tracer_provider.sampler', and the fragment of the message the
    // user must see.
    public static IEnumerable<TheoryDataRow<string, string, string>> InvalidSamplers =>
    [
        Invalid("null", "null", string.Empty, "must name exactly one sampler"),
        Invalid("scalar", "5", string.Empty, "must name exactly one sampler"),
        Invalid("sequence", "[]", string.Empty, "must name exactly one sampler"),
        Invalid("no key", "{}", string.Empty, "must name exactly one sampler"),
        Invalid("two keys", "{always_on: , always_off: }", string.Empty, "must name exactly one sampler"),
        Invalid("scalar value", "{always_on: 5}", ".always_on", "must be a mapping or null"),
    ];

    [Fact]
    public void OmittedSampler_BindsNothing() =>
        Assert.Null(Bind(null).Sampler);

    [Theory]
    [InlineData("always_off")]
    [InlineData("always_on")]
    [InlineData("always_record")]
    [InlineData("parent_based")]
    [InlineData("trace_id_ratio_based")]
    [InlineData("composite/development")]
    [InlineData("x_unknown")]
    public void Sampler_BindsByName(string name)
    {
        var sampler = Bind($"{{{name}: }}").Sampler!;

        Assert.Equal(name, sampler.Name);
        Assert.Equal($"tracer_provider.sampler.{name}", sampler.Path);
    }

    [Fact]
    public void Sampler_KeepsItsPropertiesForTheProvider()
    {
        var sampler = Bind("{trace_id_ratio_based: {ratio: 0.25}}").Sampler!;

        Assert.Equal(0.25, sampler.Properties.GetDouble("ratio").Value);
    }

    [Theory]
    [MemberData(nameof(InvalidSamplers))]
    public void InvalidSampler_ThrowsFromTheCall(string sampler, string relativePath, string expected)
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(Document(sampler));
        var builder = new ServiceCollection().AddOpenTelemetry();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() => builder.UseDeclarativeConfiguration(yamlFile.Path));

        Assert.Contains($"'tracer_provider.sampler{relativePath}' (line 5, column", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    private static TheoryDataRow<string, string, string> Invalid(string scenario, string sampler, string relativePath, string expected) =>
        new(sampler, relativePath, expected) { TestDisplayName = scenario };

    private static string Document(string? sampler) =>
        """
        file_format: "1.2"
        tracer_provider:
          processors:
            - simple: {exporter: {console: }}

        """
        + (sampler is null ? string.Empty : $"  sampler: {sampler}\n");

    private static TracerProviderSection Bind(string? sampler)
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(Document(sampler));
        var document = DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path));
        var configuration = TracerProviderConfigurationBinder.Bind(document.Properties);
        Assert.NotNull(configuration);
        return configuration;
    }
}
