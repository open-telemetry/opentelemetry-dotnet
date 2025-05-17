// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    public double[] InputBoundaries { get;  } = inputBoundaries;

    public double[] InputValues { get; } = inputValues;

    public long[] ExpectedBucketCounts { get; } = expectedBucketCounts;

    public double[] ExpectedBucketBounds { get;  } = expectedBucketBounds;

    public string TestName { get; set; } = testName;
}

