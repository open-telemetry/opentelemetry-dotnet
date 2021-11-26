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
    public enum MetricType : int
    {
        /*
        Type (bits 4,5,6,7,8,9,10,11):
            0x10 0b0000 0000 0000 0000 0000 0000 0001 0000 SUM
            0x20 0b0000 0000 0000 0000 0000 0000 0010 0000 GAUGE
            0x40 0b0000 0000 0000 0000 0000 0000 0100 0000 Histogram
            0xC0 0b0000 0000 0000 0000 0000 0000 1100 0000 HistogramWithMinMax (reserved)
            0x80 0b0000 0000 0000 0000 0000 0000 1000 0000 MinMax (reserved)
            0x50 0b0000 0000 0000 0000 0000 0000 0101 0000 Exponential Histogram (reserved)
            0x60 0b0000 0000 0000 0000 0000 0000 0110 0000 Exponential Histogram (reserved)
            0x70 0b0000 0000 0000 0000 0000 0000 0111 0000 Exponential Histogram (reserved)
            0x100 0b0000 0000 0000 0000 0000 0001 0000 0000 Summary (reserved)

        Temporality (bits 28, 29): Reserved
            0x10000000 0b0001 0000 0000 0000 0000 0000 0000 0000 Cumulative
            0x20000000 0b0010 0000 0000 0000 0000 0000 0000 0000 Delta

        Point kind (bits 0,1,2,3):
            0x04 0b0100 I1 (signed 1-byte integer)
            0x05 0b0101 U1 (unsigned 1-byte integer)
            0x06 0b0110 I2 (signed 2-byte integer)
            0x07 0b0111 U2 (unsigned 2-byte integer)
            0x08 0b1000 I4 (signed 4-byte integer)
            0x09 0b1001 U4 (unsigned 4-byte integer)
            0x0a 0b1010 I8 (signed 8-byte integer)
            0x0b 0b1011 U8 (unsigned 8-byte integer)
            0x0c 0b1100 R4 (4-byte floating point)
            0x0d 0b1101 R8 (8-byte floating point)
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
        Histogram = 0x40,

        /*
        /// <summary>
        /// Histogram with Min and Max.
        /// </summary>
        HistogramWithMinMax = 0xC0,

        /// <summary>
        /// Summary.
        /// </summary>
        Summary = 0x100,
        */
    }
}
