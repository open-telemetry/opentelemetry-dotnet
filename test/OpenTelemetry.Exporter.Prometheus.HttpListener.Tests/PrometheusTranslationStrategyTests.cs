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

    [Fact]
    public void HttpListenerOptions_DefaultTranslationStrategy_IsUnderscoreEscapingWithSuffixes()
        => Assert.Equal(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, new PrometheusHttpListenerOptions().TranslationStrategy);

    [Theory]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, EscapingScheme.Underscores)]
    [InlineData((PrometheusTranslationStrategy)int.MaxValue, EscapingScheme.Underscores)] // Unknown strategy falls back to underscores.
    internal void GetDefaultEscapingScheme_MapsEscapingAxis(PrometheusTranslationStrategy strategy, EscapingScheme expected)
        => Assert.Equal(expected, strategy.GetDefaultEscapingScheme());

    [Theory]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, false)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, true)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, true)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, false)]
    [InlineData((PrometheusTranslationStrategy)int.MaxValue, true)] // Unknown strategy falls back to appending suffixes.
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

    [Theory]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, EscapingScheme.Underscores)]
    internal void Exporter_DefaultEscapingScheme_ReflectsConfiguredStrategy(PrometheusTranslationStrategy strategy, EscapingScheme expected)
    {
        using var exporter = new PrometheusExporter(new() { TranslationStrategy = strategy });

        Assert.Equal(expected, exporter.DefaultEscapingScheme);
    }
}
