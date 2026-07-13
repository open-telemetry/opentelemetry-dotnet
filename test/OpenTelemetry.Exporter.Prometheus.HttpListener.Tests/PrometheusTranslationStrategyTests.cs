// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PrometheusTranslationStrategyTests
{
    [Fact]
    public void DefaultStrategy_IsUnderscoreEscapingWithSuffixes()
        => Assert.Equal(0, (int)PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes);

    [Fact]
    public void Exporter_DefaultOptions_AppendsSuffixes()
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());

        Assert.Equal(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, exporter.TranslationStrategy);
        Assert.True(exporter.AppendSuffixes);
    }

    [Theory]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, EscapingScheme.Underscores)]
    internal void GetDefaultEscapingScheme_MapsEscapingAxis(PrometheusTranslationStrategy strategy, EscapingScheme expected)
        => Assert.Equal(expected, strategy.GetDefaultEscapingScheme());

    [Theory]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, false)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, true)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, true)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, false)]
    internal void AppendSuffixes_MapsSuffixAxis(PrometheusTranslationStrategy strategy, bool expected)
        => Assert.Equal(expected, strategy.AppendSuffixes());

    [Theory]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, false)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, true)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, true)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, false)]
    internal void Exporter_AppendSuffixes_ReflectsConfiguredStrategy(PrometheusTranslationStrategy strategy, bool expected)
    {
        using var exporter = new PrometheusExporter(new() { TranslationStrategy = strategy });

        Assert.Equal(strategy, exporter.TranslationStrategy);
        Assert.Equal(expected, exporter.AppendSuffixes);
    }
}
