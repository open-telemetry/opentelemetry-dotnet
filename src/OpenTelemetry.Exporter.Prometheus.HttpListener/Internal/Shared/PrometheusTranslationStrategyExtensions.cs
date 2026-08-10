// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus;

internal static class PrometheusTranslationStrategyExtensions
{
    /// <summary>
    /// Gets the name escaping scheme applied by the strategy's escaping axis.
    /// </summary>
    /// <param name="strategy">The translation strategy.</param>
    /// <returns>The escaping scheme the strategy applies.</returns>
    /// <remarks>
    /// This is the escaping the exporter applies when it constructs names, before any content
    /// negotiation is taken into account. See <see cref="GetEffectiveEscapingScheme"/> for how the
    /// negotiated escaping scheme is layered on top of it. The suffix axis (see
    /// <see cref="AppendSuffixes"/>) is static and is not subject to negotiation.
    /// </remarks>
    public static EscapingScheme GetEscapingScheme(this PrometheusTranslationStrategy strategy) => strategy switch
    {
        PrometheusTranslationStrategy.NoTranslation => EscapingScheme.AllowUtf8,
        PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes => EscapingScheme.AllowUtf8,
        PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes => EscapingScheme.Underscores,
        PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes => EscapingScheme.Underscores,
        _ => EscapingScheme.Underscores,
    };

    /// <summary>
    /// Gets the escaping scheme the serialized names are rendered with, combining the escaping the
    /// strategy applies with the escaping scheme a scrape request negotiated.
    /// </summary>
    /// <param name="strategy">The translation strategy.</param>
    /// <param name="negotiated">The escaping scheme negotiated by the scrape request.</param>
    /// <returns>The escaping scheme to render names with.</returns>
    /// <remarks>
    /// <para>
    /// The specification requires the translation strategy to be applied first, with content
    /// negotiation then applying a second translation of the resulting names:
    /// https://github.com/open-telemetry/opentelemetry-specification/blob/51700bd58c79c057468b66c3fd8d075444d6140c/specification/metrics/sdk_exporters/prometheus.md#interaction-with-translation-strategy.
    /// The second translation can therefore never revert the escaping the strategy has already
    /// applied, which is what this resolves to a single scheme to render the names with.
    /// </para>
    /// <para>
    /// A strategy that escapes to <c>_</c> therefore always renders underscore-escaped names: the
    /// characters a scheme such as <c>allow-utf-8</c> would have preserved are already gone by the
    /// time negotiation is applied. The remaining schemes are not applied a second time either,
    /// because escaping an already escaped name is lossy rather than additive: the <c>dots</c>
    /// scheme, for example, would double the structural underscores the strategy introduced. The
    /// response reports the scheme returned here rather than the one that was negotiated, so it
    /// always describes the names that were written. A strategy that passes UTF-8 names through,
    /// on the other hand, has not escaped anything yet and so defers entirely to the negotiated
    /// scheme.
    /// </para>
    /// </remarks>
    public static EscapingScheme GetEffectiveEscapingScheme(this PrometheusTranslationStrategy strategy, EscapingScheme negotiated)
        => strategy.GetEscapingScheme() == EscapingScheme.AllowUtf8 ? negotiated : EscapingScheme.Underscores;

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
