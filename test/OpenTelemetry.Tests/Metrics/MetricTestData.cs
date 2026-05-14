// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

internal static class MetricTestData
{
#pragma warning disable CA1825 // Workaround for https://github.com/dotnet/sdk/issues/54275
    public static TheoryData<string> InvalidInstrumentNames =>
    [
        " ",
        "  ",
        " leading-space",
        "trailing-space ",
        "invalid+separator",
        "invalid[separator",
        "invalid;separator",
        "invalid,separator",
        "invalid=separator",
        "invalid?separator",
        new('m', 256),
        "a\xb5", // `\xb5` is the Micro character (non-ASCII)
        "name\twith\ttab",
        "name\nwith\nnewline",
    ];

    public static TheoryData<string> ValidInstrumentNames =>
    [
        "m",
        "first-char-alphabetic",
        "my-2-instrument",
        "my.metric",
        "my_metric2",
        new('m', 255),
        "CaSe-InSeNsItIvE",
        "my_metric/environment/database",

        // Newly valid characters: ':', '\', '(', ')', '%', '*', '#', and space.
        // See https://github.com/open-telemetry/opentelemetry-specification/pull/5092.
        "with:colon",
        @"with\backslash",
        "with(parens)",
        "with%percent",
        "with*asterisk",
        "with#hash",
        "name with spaces",
        "name with  consecutive  spaces",

        // Newly valid: leading character no longer required to be alphabetic.
        "-leadingDash",
        ".leadingDot",
        "1leadingDigit",
        "_leadingUnderscore",
        ":leadingColon",
        @"\leadingBackslash",
        "(leadingParen)",
        "%leadingPercent",
        "*leadingAsterisk",
        "#leadingHash",

        // Real-world Windows performance counter style names.
        @"\Processor(_Total)\% Processor Time",
        @"\Memory\Available Bytes",
        @"\PhysicalDisk(_Total)\Avg. Disk Queue Length",
        @"\Network Interface(*)\Bytes Total/sec",

        // Real-world .NET CLR performance counter style names.
        @"\.NET CLR Memory(*)\# Bytes in all Heaps",
        @"\.NET CLR Memory(_Global_)\# Gen 0 Collections",
    ];

    public static TheoryData<double[]> InvalidHistogramBoundaries =>
    [
        [0.0, 0.0],
        [1.0, 0.0],
        [0.0, 1.0, 1.0, 2.0],
        [0.0, 1.0, 2.0, -1.0],
    ];
#pragma warning restore CA1825 // Workaround for https://github.com/dotnet/sdk/issues/54275

    public static TheoryData<double[], HistogramConfiguration, double, double> ValidHistogramMinMax => new()
    {
        { [-10.0, 0.0, 1.0, 9.0, 10.0, 11.0, 19.0], new HistogramConfiguration(), -10.0, 19.0 },
        { [double.NegativeInfinity], new HistogramConfiguration(), double.NegativeInfinity, double.NegativeInfinity },
        { [double.NegativeInfinity, 0.0, double.PositiveInfinity], new HistogramConfiguration(), double.NegativeInfinity, double.PositiveInfinity },
        { [1.0], new HistogramConfiguration(), 1.0, 1.0 },
        { [5.0, 100.0, 4.0, 101.0, -2.0, 97.0], new ExplicitBucketHistogramConfiguration { Boundaries = [10.0, 20.0] }, -2.0, 101.0 },
        { [5.0, 100.0, 4.0, 101.0, -2.0, 97.0], new Base2ExponentialBucketHistogramConfiguration(), 4.0, 101.0 },
    };

    public static TheoryData<double[], HistogramConfiguration> InvalidHistogramMinMax => new()
    {
        { [1.0], new HistogramConfiguration { RecordMinMax = false } },
        { [1.0], new ExplicitBucketHistogramConfiguration { Boundaries = [10.0, 20.0], RecordMinMax = false } },
        { [1.0], new Base2ExponentialBucketHistogramConfiguration { RecordMinMax = false } },
    };
}
