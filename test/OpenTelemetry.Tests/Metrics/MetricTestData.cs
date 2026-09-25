// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics.Tests;

#pragma warning disable CA1861 // Avoid constant arrays as arguments

internal static class MetricTestData
{
    public static TheoryData<string> InvalidInstrumentNames =>
    [
        " ",
        "-first-char-not-alphabetic",
        "1first-char-not-alphabetic",
        "invalid+separator",
        new string('m', 256),
        "a\xb5", // `\xb5` is the Micro character
    ];

    public static TheoryData<string> ValidInstrumentNames =>
    [
        "m",
        "first-char-alphabetic",
        "my-2-instrument",
        "my.metric",
        "my_metric2",
        new string('m', 255),
        "CaSe-InSeNsItIvE",
        "my_metric/environment/database",
    ];

    public static TheoryData<double[]> InvalidHistogramBoundaries =>
    [
        new double[] { 0.0, 0.0 },
        new double[] { 1.0, 0.0 },
        new double[] { 0.0, 1.0, 1.0, 2.0 },
        new double[] { 0.0, 1.0, 2.0, -1.0 },
        new double[] { double.NaN },
        new double[] { 0.0, double.NaN, 1.0 },
    ];

    public static TheoryData<double[], HistogramConfiguration, double, double> ValidHistogramMinMax => new()
    {
        { new double[] { -10.0, 0.0, 1.0, 9.0, 10.0, 11.0, 19.0 }, new HistogramConfiguration(), -10.0, 19.0 },
        { new double[] { double.NegativeInfinity }, new HistogramConfiguration(), double.NegativeInfinity, double.NegativeInfinity },
        { new double[] { double.NegativeInfinity, 0.0, double.PositiveInfinity }, new HistogramConfiguration(), double.NegativeInfinity, double.PositiveInfinity },
        { new double[] { 1.0 }, new HistogramConfiguration(), 1.0, 1.0 },
        { new double[] { 5.0, 100.0, 4.0, 101.0, -2.0, 97.0 }, new ExplicitBucketHistogramConfiguration { Boundaries = [10.0, 20.0] }, -2.0, 101.0 },
        { new double[] { 5.0, 100.0, 4.0, 101.0, -2.0, 97.0 }, new Base2ExponentialBucketHistogramConfiguration(), 4.0, 101.0 },
    };

    public static TheoryData<double[], HistogramConfiguration> InvalidHistogramMinMax => new()
    {
        { new double[] { 1.0 }, new HistogramConfiguration { RecordMinMax = false } },
        { new double[] { 1.0 }, new ExplicitBucketHistogramConfiguration { Boundaries = [10.0, 20.0], RecordMinMax = false } },
        { new double[] { 1.0 }, new Base2ExponentialBucketHistogramConfiguration { RecordMinMax = false } },
    };
}
