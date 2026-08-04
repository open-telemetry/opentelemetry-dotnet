// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// The numeric type-parameter family of an instrument, per the (i)/(f) markers in
/// the #2618 compatibility table.
/// </summary>
internal enum NumericKind
{
    /// <summary>
    /// The instrument's value type parameter is not recognized as either
    /// <see cref="Integral"/> or <see cref="Floating"/> (e.g. <c>decimal</c>).
    /// Aggregations that require a specific numeric width are not compatible
    /// with an <see cref="Unknown"/> numeric kind.
    /// </summary>
    Unknown,

    /// <summary>
    /// The instrument's value type parameter is <c>long</c>, <c>int</c>,
    /// <c>short</c>, or <c>byte</c>.
    /// </summary>
    Integral,

    /// <summary>
    /// The instrument's value type parameter is <c>double</c> or <c>float</c>.
    /// </summary>
    Floating,
}
