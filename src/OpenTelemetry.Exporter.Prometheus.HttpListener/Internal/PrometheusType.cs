// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus;

internal enum PrometheusType
{
    /// <summary>
    /// Not mapped.
    /// </summary>
    Untyped,

    /// <summary>
    /// Mapped from Gauge and UpDownCounter.
    /// </summary>
    Gauge,

    /// <summary>
    /// Mapped from Counter.
    /// </summary>
    Counter,

    /// <summary>
    /// Not mapped.
    /// </summary>
    Summary,

    /// <summary>
    /// Mapped from Histogram.
    /// </summary>
    Histogram,
}