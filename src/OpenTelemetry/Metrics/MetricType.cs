// <copyright file="MetricType.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;

namespace OpenTelemetry.Metrics
{
    [Flags]
    public enum MetricType : short
    {
        /*
        Type:
            0x10  0b0000 0001 0000 SUM
            0x20  0b0000 0010 0000 GAUGE

            0x80  0b0000 1000 0000 Histogram
            0x90  0b0000 1001 0000 HistogramWithBuckets
            0xA0  0b0000 1010 0000 HistogramWithMinMax
            0xB0  0b0000 1011 0000 HistogramWithMinMaxAndBuckets

            0xC0  0b0000 1100 0000 Exponential Histogram

            0x100 0b0001 0000 0000 Summary

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
        /// Histogram. (Sum and Count).
        /// </summary>
        Histogram = 0x80,

        /// <summary>
        /// Histogram with Buckets.
        /// </summary>
        HistogramWithBuckets = 0x90,

        /*
        /// <summary>
        /// Histogram with Min and Max.
        /// </summary>
        HistogramWithMinMax = 0xA0,

        /// <summary>
        /// Histogram with Min, Max and Buckets.
        /// </summary>
        HistogramWithMinMaxAndBuckets = 0xB0,

        /// <summary>
        /// Exponential Histogram.
        /// </summary>
        ExponentialHistogram = 0xC0,

        /// <summary>
        /// Summary.
        /// </summary>
        Summary = 0x100,
        */
    }
}
