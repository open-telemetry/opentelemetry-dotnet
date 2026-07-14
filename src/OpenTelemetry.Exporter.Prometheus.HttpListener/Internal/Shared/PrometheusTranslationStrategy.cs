// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Controls how OpenTelemetry metric and label names are translated into Prometheus names.
/// </summary>
/// <remarks>
/// This enum models the OpenTelemetry specification's <c>translation_strategy</c> option.
/// </remarks>
internal enum PrometheusTranslationStrategy
{
    /// <summary>
    /// Discouraged characters are escaped to <c>_</c> and unit and type (e.g. <c>_total</c>)
    /// suffixes are appended. This is the default and matches the classic Prometheus behaviour.
    /// </summary>
    UnderscoreEscapingWithSuffixes = 0,

    /// <summary>
    /// Discouraged characters are escaped to <c>_</c> but no unit or type suffixes are appended.
    /// </summary>
    UnderscoreEscapingWithoutSuffixes,

    /// <summary>
    /// Names are not escaped (UTF-8 is passed through) and unit and type suffixes are appended.
    /// </summary>
    NoUTF8EscapingWithSuffixes,

    /// <summary>
    /// Names are passed through completely unaltered: names are not escaped and no suffixes are appended.
    /// </summary>
    NoTranslation,
}
