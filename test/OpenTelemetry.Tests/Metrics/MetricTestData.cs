// <copyright file="MetricTestData.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Metrics.Tests;

public class MetricTestData
{
    public static IEnumerable<object[]> InvalidInstrumentNames
       => new List<object[]>
       {
                new object[] { " " },
                new object[] { "-first-char-not-alphabetic" },
                new object[] { "1first-char-not-alphabetic" },
                new object[] { "invalid+separator" },
                new object[] { new string('m', 64) },
       };

    public static IEnumerable<object[]> ValidInstrumentNames
       => new List<object[]>
       {
                new object[] { "m" },
                new object[] { "first-char-alphabetic" },
                new object[] { "my-2-instrument" },
                new object[] { "my.metric" },
                new object[] { "my_metric2" },
                new object[] { new string('m', 63) },
       };

    public static IEnumerable<object[]> InvalidHistogramBoundaries
       => new List<object[]>
       {
                new object[] { new double[] { 0, 0 } }, // Not distinct
                new object[] { new double[] { 1, 0 } }, // Descending order
                new object[] { new double[] { 0, 1, 1, 2 } }, // Not distinct
                new object[] { new double[] { 0, 1, 2, -1 } }, // Descending order
                new object[] { new double[] { double.NaN } }, // NaN
                new object[] { new double[] { 0, 1, 2, double.NegativeInfinity } }, // Descending order -Inf
                new object[] { new double[] { 0, 1, 2, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity } }, // Not distinct, +Inf
                new object[] { new double[] { double.PositiveInfinity, 0, 1, 2 } }, // Descending order
       };

    public static IEnumerable<object[]> ValidHistogramData
       => new List<object[]>
       {
                new object[] // basic custom bounds
                {
                    new double[] { 10, 20 },
                    new double[] { -10, 0, 1, 9, 10, 11, 19 },
                    40,
                    new long[] { 5, 2, 0 },
                },
                new object[] // null bounds will use default bounds
                {
                    null,
                    new double[] { -10, 0, 1, 9, 10, 11, 19 },
                    40,
                    new long[] { 2, 1, 2, 2, 0, 0, 0, 0, 0, 0, 0 },
                },
                new object[] // infinite bounds will be ignored
                {
                    new double[] { double.NegativeInfinity, 5 },
                    new double[] { -10, 0, 1, 9, 10, 11, 19 },
                    40,
                    new long[] { 3, 4 },
                },
                new object[] // only infinity bounds will only record sum and count
                {
                    new double[] { double.NegativeInfinity, double.PositiveInfinity },
                    new double[] { -10, 0, 1, 9, 10, 11, 19 },
                    40,
                    new long[] { 7 },
                },
       };
}
