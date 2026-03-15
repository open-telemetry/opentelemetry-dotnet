// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests;

public class Base2ExponentialBucketHistogramHelperTests
{
    private readonly ITestOutputHelper output;

    public Base2ExponentialBucketHistogramHelperTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public static TheoryData<int> GetNonPositiveScales()
    {
#pragma warning disable CA1825 // HACK Workaround for https://github.com/dotnet/sdk/issues/53047
        TheoryData<int> theoryData = [];
#pragma warning restore CA1825
        for (var i = -11; i <= 0; ++i)
        {
            theoryData.Add(i);
        }

        return theoryData;
    }

    public static TheoryData<int> GetPositiveScales()
    {
#pragma warning disable CA1825 // HACK Workaround for https://github.com/dotnet/sdk/issues/53047
        TheoryData<int> theoryData = [];
#pragma warning restore CA1825
        for (var i = 1; i <= 20; ++i)
        {
            theoryData.Add(i);
        }

        return theoryData;
    }

    [Theory]
    [MemberData(nameof(GetNonPositiveScales))]
    public void TestNonPositiveScalesLowerBoundaryRoundTrip(int scale)
    {
        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var minIndex = histogram.MapToIndex(double.Epsilon);
        var maxIndex = histogram.MapToIndex(double.MaxValue);

        for (var index = minIndex; index <= maxIndex; ++index)
        {
            var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(index, scale);
            var roundTrip = histogram.MapToIndex(lowerBound);

            if (lowerBound == double.Epsilon)
            {
                // The minimum index is inclusive of double.Epsilon.
                Assert.Equal(index, roundTrip);
            }
            else if ((scale == 0 && index == -1074) || (scale == -1 && index == -537))
            {
                /*
                These are unique cases in that these buckets near the
                minimum index have a lower inclusive bound:

                Scale 0:
                    bucket[-1075]: [double.Epsilon, double.Epsilon]
                    bucket[-1074]: [double.Epsilon * 2, double.Epsilon * 2]
                    ...

                Scale -1:
                    bucket[-538]: [double.Epsilon, double.Epsilon]
                    bucket[-537]: [double.Epsilon * 2, double.Epsilon * 4]
                    ...
                */
                Assert.Equal(index, roundTrip);
            }
            else
            {
                // In the most common situation, the lower boundary of a bucket
                // is exclusive. That is:
                //     MapToIndex(LowerBoundary(index)) == index - 1
                Assert.Equal(index - 1, roundTrip);
                roundTrip = histogram.MapToIndex(MathHelper.BitIncrement(lowerBound));
                Assert.Equal(index, roundTrip);
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetPositiveScales))]
    public void TestPositiveScalesLowerBoundaryRoundTripPowersOfTwo(int scale)
    {
        /*
        MapToIndex and LowerBoundary methods can be imprecise at positive
        scales. This test provides an analysis for where MapToIndex is
        off by one relative to LowerBoundary by performing a "round trip".

        The expectation is that:
            MapToIndex(LowerBoundary(index)) = index - 1;

        However, in some circumstances it will incorrectly be:
            MapToIndex(LowerBoundary(index)) = index;
        */

        // Set this variable to true to generate output for analysis.
        var displayDebugInfo = false;
        this.DisplayHeader(displayDebugInfo);

        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var minIndex = histogram.MapToIndex(double.Epsilon);
        var maxIndex = histogram.MapToIndex(double.MaxValue);
        var indexesPerPowerOf2 = 1 << scale;

        for (var index = -indexesPerPowerOf2; index > minIndex; index -= indexesPerPowerOf2)
        {
            var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(index, scale);
            var roundTrip = histogram.MapToIndex(lowerBound);

            // The round trip is off by one.
            if (index == roundTrip)
            {
                this.DisplayMarginOfError(displayDebugInfo, scale, index);
            }

            // The round trip is correct.
            else if (index - 1 == roundTrip)
            {
                // However, the lower bound computed may not be the precise lower bound.
                this.DisplayMarginOfError(displayDebugInfo, scale, index);
            }

            // Something is very wrong.
            else
            {
                Assert.Fail($"{index} - 1 != {roundTrip} && {index} != {roundTrip}");
            }
        }

        for (var index = indexesPerPowerOf2; index < maxIndex; index += indexesPerPowerOf2)
        {
            var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(index, scale);
            var roundTrip = histogram.MapToIndex(lowerBound);

            // The round trip is never off by one for positive indexes near powers of two.
            Assert.Equal(index - 1, roundTrip);

            // However, the lower bound computed may not be the precise lower bound.
            this.DisplayMarginOfError(displayDebugInfo, scale, index);
        }
    }

    [Theory]
    [MemberData(nameof(GetPositiveScales))]
    public void TestPositiveScalesLowerBoundaryMaxIndex(int scale)
    {
        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var maxIndex = histogram.MapToIndex(double.MaxValue);

        var lowerBoundary = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(maxIndex, scale);
        var roundTrip = histogram.MapToIndex(lowerBoundary);
        Assert.Equal(maxIndex - 1, roundTrip);
    }

    [Theory]
    [MemberData(nameof(GetPositiveScales))]
    public void TestPositiveScalesLowerBoundaryMinIndex(int scale)
    {
        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var minIndex = histogram.MapToIndex(double.Epsilon);

        var lowerBoundary = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(minIndex, scale);
        var roundTrip = histogram.MapToIndex(lowerBoundary);
        Assert.Equal(minIndex, roundTrip);
    }

    private void DisplayHeader(bool displayDebugInfo)
    {
        if (!displayDebugInfo)
        {
            return;
        }

        this.output.WriteLine(string.Empty);
        this.output.WriteLine("scale,index,unusedIndex,LowerBound(index),MapToIndex(LowerBound(index)),preciseLowerBound,lowerBoundDelta,marginOfError,ops");
    }

    private void DisplayMarginOfError(bool displayDebugInfo, int scale, int index)
    {
        if (!displayDebugInfo)
        {
            return;
        }

        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(index, scale);
        var roundTrip = histogram.MapToIndex(lowerBound);

        Assert.True((index == roundTrip) || (index - 1 == roundTrip));

        var preciseLowerBound = lowerBound;
        var newRoundTrip = roundTrip;
        var increments = 0;
        var unusedIndex = false;

        if (index == roundTrip)
        {
            for (; newRoundTrip != index - 1;)
            {
                preciseLowerBound = MathHelper.BitDecrement(preciseLowerBound);
                newRoundTrip = histogram.MapToIndex(preciseLowerBound);
                ++increments;
            }
        }
        else
        {
            for (; newRoundTrip < index;)
            {
                var newLowerBound = MathHelper.BitIncrement(preciseLowerBound);
                newRoundTrip = histogram.MapToIndex(newLowerBound);

                if (newRoundTrip < index)
                {
                    preciseLowerBound = newLowerBound;
                    ++increments;
                }
            }

            // This represents an index that MapToIndex will never map to.
            // This occurs for negative indexes very near the minimum index.
            if (newRoundTrip != index)
            {
                unusedIndex = true;
            }
        }

        var lowerBoundDelta = preciseLowerBound - lowerBound;
        var marginOfError = lowerBoundDelta / lowerBound;
        this.output.WriteLine($"{scale},{index},{unusedIndex},{lowerBound},{roundTrip},{preciseLowerBound},{lowerBoundDelta},{marginOfError},{increments}");
    }
}
