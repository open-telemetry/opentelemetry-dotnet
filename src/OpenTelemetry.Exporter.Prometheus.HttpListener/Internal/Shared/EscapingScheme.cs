// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// The Prometheus UTF-8 name escaping scheme used when rendering metric and label names.
/// See https://prometheus.io/docs/instrumenting/escaping_schemes/.
/// </summary>
internal enum EscapingScheme
{
    /// <summary>
    /// Discouraged characters are replaced with the <c>_</c> character. This is the
    /// OpenTelemetry default and also covers the legacy (pre-1.0.0) text formats which
    /// have no negotiated escaping scheme.
    /// </summary>
    Underscores = 0,

    /// <summary>
    /// Dots are replaced with <c>_dot_</c> and underscores are doubled, allowing the
    /// original name to be recovered by a Prometheus client.
    /// </summary>
    Dots,

    /// <summary>
    /// Names that are not valid legacy names are prefixed with <c>U__</c> and discouraged
    /// characters are encoded as their hexadecimal Unicode code point.
    /// </summary>
    Values,
}
