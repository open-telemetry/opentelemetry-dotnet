// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus;

internal static class PrometheusTranslationStrategyExtensions
{
    /// <summary>
    /// Gets the default name escaping scheme implied by the strategy's escaping axis.
    /// </summary>
    /// <param name="strategy">The translation strategy.</param>
    /// <returns>The escaping scheme the strategy defaults to.</returns>
    /// <remarks>
    /// This is the escaping scheme the exporter intends to use when a scrape request does not
    /// negotiate one of its own. It is not yet consumed: layering content negotiation on top of
    /// this default (so a negotiated <c>escaping=</c> preference overrides it) is deferred to a
    /// follow-up change. The suffix axis (see <see cref="AppendSuffixes"/>) is static and is not
    /// subject to negotiation.
    /// </remarks>
    public static EscapingScheme GetDefaultEscapingScheme(this PrometheusTranslationStrategy strategy) => strategy switch
    {
        PrometheusTranslationStrategy.NoTranslation => EscapingScheme.AllowUtf8,
        PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes => EscapingScheme.AllowUtf8,
        PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes => EscapingScheme.Underscores,
        PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes => EscapingScheme.Underscores,
        _ => EscapingScheme.Underscores,
    };

    /// <summary>
    /// Gets a value indicating whether unit and type (e.g. <c>_total</c>) suffixes are appended to
    /// metric names.
    /// </summary>
    /// <param name="strategy">The translation strategy.</param>
    /// <returns>
    /// <see langword="true"/> if unit and type suffixes are appended; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool AppendSuffixes(this PrometheusTranslationStrategy strategy) => strategy switch
    {
        PrometheusTranslationStrategy.NoTranslation => false,
        PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes => true,
        PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes => true,
        PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes => false,
        _ => true,
    };
}
