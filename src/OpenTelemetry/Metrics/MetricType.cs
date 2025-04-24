// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Enumeration used to define the type of a <see cref="Metric"/>.
/// </summary>
[Flags]
#pragma warning disable CA1028 // Enum storage should be Int32
#pragma warning disable CA2217 // Do not mark enums with FlagsAttribute
public enum MetricType : byte
#pragma warning restore CA2217 // Do not mark enums with FlagsAttribute
#pragma warning restore CA1028 // Enum storage should be Int32
{
    /*
    Type:
        0x10: Sum
        0x20: Gauge
        0x30: Summary (reserved)
        0x40: Histogram
        0x50: ExponentialHistogram
        0x60: (unused)
        0x70: (unused)
        0x80: SumNonMonotonic

    Point kind:
        0x04: I1 (signed 1-byte integer)
        0x05: U1 (unsigned 1-byte integer)
        0x06: I2 (signed 2-byte integer)
        0x07: U2 (unsigned 2-byte integer)
        0x08: I4 (signed 4-byte integer)
        0x09: U4 (unsigned 4-byte integer)
        0x0a: I8 (signed 8-byte integer)
        0x0b: U8 (unsigned 8-byte integer)
        0x0c: R4 (4-byte floating point)
        0x0d: R8 (8-byte floating point)
    */

    /// <summary>
    /// Sum of Long type.
    /// </summary>
    LongSum = 0x1a,

    /// <summary>
    /// Sum of Double type.
    /// </summary>
    DoubleSum = 0x1d,

    /// <summary>
    /// Gauge of Long type.
    /// </summary>
    LongGauge = 0x2a,

    /// <summary>
    /// Gauge of Double type.
    /// </summary>
    DoubleGauge = 0x2d,

    /// <summary>
    /// Histogram.
    /// </summary>
    Histogram = 0x40,

    /// <summary>
    /// Exponential Histogram.
    /// </summary>
    ExponentialHistogram = 0x50,

    /// <summary>
    /// Non-monotonic Sum of Long type.
    /// </summary>
    LongSumNonMonotonic = 0x8a,

    /// <summary>
    /// Non-monotonic Sum of Double type.
    /// </summary>
    DoubleSumNonMonotonic = 0x8d,
}
