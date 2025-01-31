// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
                new object[] { new string('m', 256) },
                new object[] { "a\xb5" }, // `\xb5` is the Micro character
       };

    public static IEnumerable<object[]> ValidInstrumentNames
       => new List<object[]>
       {
                new object[] { "m" },
                new object[] { "first-char-alphabetic" },
                new object[] { "my-2-instrument" },
                new object[] { "my.metric" },
                new object[] { "my_metric2" },
                new object[] { new string('m', 255) },
                new object[] { "CaSe-InSeNsItIvE" },
                new object[] { "my_metric/environment/database" },
       };

    public static IEnumerable<object[]> InvalidHistogramBoundaries
       => new List<object[]>
       {
                new object[] { new double[] { 0, 0 } },
                new object[] { new double[] { 1, 0 } },
                new object[] { new double[] { 0, 1, 1, 2 } },
                new object[] { new double[] { 0, 1, 2, -1 } },
       };

    public static IEnumerable<object[]> ValidHistogramMinMax
       => new List<object[]>
       {
                new object[] { new double[] { -10, 0, 1, 9, 10, 11, 19 }, new HistogramConfiguration(), -10, 19 },
                new object[] { new double[] { double.NegativeInfinity }, new HistogramConfiguration(), double.NegativeInfinity, double.NegativeInfinity },
                new object[] { new double[] { double.NegativeInfinity, 0, double.PositiveInfinity }, new HistogramConfiguration(), double.NegativeInfinity, double.PositiveInfinity },
                new object[] { new double[] { 1 }, new HistogramConfiguration(), 1, 1 },
                new object[] { new double[] { 5, 100, 4, 101, -2, 97 }, new ExplicitBucketHistogramConfiguration() { Boundaries = [10, 20] }, -2, 101 },
                new object[] { new double[] { 5, 100, 4, 101, -2, 97 }, new Base2ExponentialBucketHistogramConfiguration(), 4, 101 },
       };

    public static IEnumerable<object[]> InvalidHistogramMinMax
       => new List<object[]>
       {
                new object[] { new double[] { 1 }, new HistogramConfiguration() { RecordMinMax = false } },
                new object[] { new double[] { 1 }, new ExplicitBucketHistogramConfiguration() { Boundaries = [10, 20], RecordMinMax = false } },
                new object[] { new double[] { 1 }, new Base2ExponentialBucketHistogramConfiguration() { RecordMinMax = false } },
       };
}
