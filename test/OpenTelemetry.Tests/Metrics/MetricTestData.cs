// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

internal static class MetricTestData
{
#pragma warning disable CA1825 // Workaround false positive in .NET 11
    public static TheoryData<string> InvalidInstrumentNames
       =>
       [
                " ",
                "-first-char-not-alphabetic",
                "1first-char-not-alphabetic",
                "invalid+separator",
                new('m', 256),
                "a\xb5", // `\xb5` is the Micro character
       ];

    public static TheoryData<string> ValidInstrumentNames
       =>
       [
                "m",
                "first-char-alphabetic",
                "my-2-instrument",
                "my.metric",
                "my_metric2",
                new('m', 255),
                "CaSe-InSeNsItIvE",
                "my_metric/environment/database",
       ];

    public static TheoryData<double[]> InvalidHistogramBoundaries
       =>
       [
                [0.0, 0.0],
                [1.0, 0.0],
                [0.0, 1.0, 1.0, 2.0],
                [0.0, 1.0, 2.0, -1.0],
       ];
#pragma warning restore CA1825

    public static TheoryData<double[], HistogramConfiguration, double, double> ValidHistogramMinMax =>
        new()
        {
            { [-10.0, 0.0, 1.0, 9.0, 10.0, 11.0, 19.0], new HistogramConfiguration(), -10.0, 19.0 },
            { [double.NegativeInfinity], new HistogramConfiguration(), double.NegativeInfinity, double.NegativeInfinity },
            { [double.NegativeInfinity, 0.0, double.PositiveInfinity], new HistogramConfiguration(), double.NegativeInfinity, double.PositiveInfinity },
            { [1.0], new HistogramConfiguration(), 1.0, 1.0 },
            { [5.0, 100.0, 4.0, 101.0, -2.0, 97.0], new ExplicitBucketHistogramConfiguration { Boundaries = [10.0, 20.0] }, -2.0, 101.0 },
            { [5.0, 100.0, 4.0, 101.0, -2.0, 97.0], new Base2ExponentialBucketHistogramConfiguration(), 4.0, 101.0 },
        };

    public static TheoryData<double[], HistogramConfiguration> InvalidHistogramMinMax
       => new()
       {
            { [1.0], new HistogramConfiguration { RecordMinMax = false } },
            { [1.0], new ExplicitBucketHistogramConfiguration { Boundaries = [10.0, 20.0], RecordMinMax = false } },
            { [1.0], new Base2ExponentialBucketHistogramConfiguration { RecordMinMax = false } },
       };
}
