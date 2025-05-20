// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

#pragma warning disable CA1819 // Properties should not return arrays
#pragma warning disable CA1515 // Consider making public types internal
public class HistogramBoundaryTestCase(
#pragma warning restore CA1515 // Consider making public types internal
    string testName,
    double[] inputBoundaries,
    double[] inputValues,
    long[] expectedBucketCounts,
    double[] expectedBucketBounds)
{
    public double[] InputBoundaries { get; } = inputBoundaries;

    public double[] InputValues { get; } = inputValues;

    public long[] ExpectedBucketCounts { get; } = expectedBucketCounts;

    public double[] ExpectedBucketBounds { get; } = expectedBucketBounds;

    public string TestName { get; set; } = testName;

    public static TheoryData<HistogramBoundaryTestCase> HistogramInfinityBoundariesTestCases()
    {
        var data = new TheoryData<HistogramBoundaryTestCase>
        {
            new(
                testName: "Custom boundaries with no infinity in explicit boundaries",
                inputBoundaries: [0, 10],
                inputValues: [-10, 0, 5, 10, 100],
                expectedBucketCounts: [2, 2, 1],
                expectedBucketBounds: [0, 10, double.PositiveInfinity]),

            new(
                testName: "Custom boundaries with positive infinity",
                inputBoundaries: [0, double.PositiveInfinity],
                inputValues: [-10, 0, 10, 100],
                expectedBucketCounts: [2, 2],
                expectedBucketBounds: [0, double.PositiveInfinity]),

            new(
                testName: "Custom boundaries with negative infinity",
                inputBoundaries: [double.NegativeInfinity, 0, 10],
                inputValues: [-100, -10, 0, 5, 10, 100],
                expectedBucketCounts: [3, 2, 1],
                expectedBucketBounds: [0, 10, double.PositiveInfinity]),

            new(
                testName: "Custom boundaries with both infinities",
                inputBoundaries: [double.NegativeInfinity, 0, 10, double.PositiveInfinity],
                inputValues: [-100, -10, 0, 5, 10, 100],
                expectedBucketCounts: [3, 2, 1],
                expectedBucketBounds: [0, 10, double.PositiveInfinity]),

            new(
                testName: "Custom boundaries with infinities only",
                inputBoundaries: [double.NegativeInfinity, double.PositiveInfinity],
                inputValues: [-10, 0, 10],
                expectedBucketCounts: [3],
                expectedBucketBounds: [double.PositiveInfinity]),
        };

        return data;
    }
}

