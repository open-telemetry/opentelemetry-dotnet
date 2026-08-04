// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// The instrument kinds recognized by <see cref="AggregationCompatibility"/>.
/// Mirrors the seven instrument kinds in the #2618 compatibility table.
/// </summary>
internal enum InstrumentKind
{
    /// <summary>
    /// A synchronous, monotonic <c>Counter&lt;T&gt;</c>.
    /// </summary>
    Counter,

    /// <summary>
    /// A synchronous, non-monotonic <c>UpDownCounter&lt;T&gt;</c>.
    /// </summary>
    UpDownCounter,

    /// <summary>
    /// A synchronous <c>Histogram&lt;T&gt;</c>.
    /// </summary>
    Histogram,

    /// <summary>
    /// A synchronous <c>Gauge&lt;T&gt;</c>.
    /// </summary>
    Gauge,

    /// <summary>
    /// An asynchronous, monotonic <c>ObservableCounter&lt;T&gt;</c>.
    /// </summary>
    ObservableCounter,

    /// <summary>
    /// An asynchronous, non-monotonic <c>ObservableUpDownCounter&lt;T&gt;</c>.
    /// </summary>
    ObservableUpDownCounter,

    /// <summary>
    /// An asynchronous <c>ObservableGauge&lt;T&gt;</c>.
    /// </summary>
    ObservableGauge,
}
