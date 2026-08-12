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
    internal void GetEscapingScheme_MapsEscapingAxis(PrometheusTranslationStrategy strategy, EscapingScheme expected)
        => Assert.Equal(expected, strategy.GetEscapingScheme());

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
    //// A strategy that passes UTF-8 names through defers entirely to the negotiated scheme
    [InlineData(PrometheusTranslationStrategy.NoTranslation, EscapingScheme.AllowUtf8, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, EscapingScheme.Underscores, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, EscapingScheme.Dots, EscapingScheme.Dots)]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, EscapingScheme.Values, EscapingScheme.Values)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, EscapingScheme.AllowUtf8, EscapingScheme.AllowUtf8)]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, EscapingScheme.Underscores, EscapingScheme.Underscores)]
    //// A strategy that escapes to '_' has already discarded the original characters, so no negotiated scheme can revert it
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, EscapingScheme.AllowUtf8, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, EscapingScheme.Underscores, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, EscapingScheme.Dots, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, EscapingScheme.Values, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, EscapingScheme.AllowUtf8, EscapingScheme.Underscores)]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, EscapingScheme.Underscores, EscapingScheme.Underscores)]
    internal void GetEffectiveEscapingScheme_CombinesStrategyWithNegotiatedScheme(
        PrometheusTranslationStrategy strategy,
        EscapingScheme negotiated,
        EscapingScheme expected)
        => Assert.Equal(expected, strategy.GetEffectiveEscapingScheme(negotiated));

    // See https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk_exporters/prometheus.md#interaction-with-translation-strategy
    [Theory]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, null, "foo_bar_bytes_total")]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, "underscores", "foo_bar_bytes_total")]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, "allow-utf-8", "foo_bar_bytes_total")]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, null, "foo_bar")]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, "underscores", "foo_bar")]
    [InlineData(PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes, "allow-utf-8", "foo_bar")]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, null, "foo_bar_bytes_total")]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, "underscores", "foo_bar_bytes_total")]
    [InlineData(PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes, "allow-utf-8", "foo.bar_bytes_total")]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, null, "foo_bar")]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, "underscores", "foo_bar")]
    [InlineData(PrometheusTranslationStrategy.NoTranslation, "allow-utf-8", "foo.bar")]
    internal void MetricName_MatchesSpecification(PrometheusTranslationStrategy strategy, string? escaping, string expected)
    {
        var accept = escaping is null
            ? "text/plain; version=1.0.0"
            : $"text/plain; version=1.0.0; escaping={escaping}";

        var protocol = PrometheusHeadersParser.Negotiate(accept);

        var metric = new PrometheusMetric(
            "foo.bar",
            "By",
            PrometheusType.Counter,
            disableTotalNameSuffixForCounters: false,
            appendSuffixes: strategy.AppendSuffixes());

        var names = metric.GetNameSet(strategy.GetEffectiveEscapingScheme(protocol.EscapingScheme));

        Assert.Equal(expected, names.Name);
    }
}
