// <copyright file="Base2ExponentialBucketHistogram.LowerBoundary.Test.cs" company="OpenTelemetry Authors">
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

using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests;

public partial class Base2ExponentialBucketHistogramTest
{
    private readonly ITestOutputHelper output;

    public Base2ExponentialBucketHistogramTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    public static IEnumerable<object[]> GetNonPositiveScales()
    {
        for (var i = -11; i <= 0; ++i)
        {
            yield return new object[] { i };
        }
    }

    public static IEnumerable<object[]> GetPositiveScales()
    {
        for (var i = 1; i <= 20; ++i)
        {
            yield return new object[] { i };
        }
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
            var lowerBound = Base2ExponentialBucketHistogram.LowerBoundary(index, scale);
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
                roundTrip = histogram.MapToIndex(BitIncrement(lowerBound));
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
            var lowerBound = Base2ExponentialBucketHistogram.LowerBoundary(index, scale);
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
            var lowerBound = Base2ExponentialBucketHistogram.LowerBoundary(index, scale);
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

        var lowerBoundary = Base2ExponentialBucketHistogram.LowerBoundary(maxIndex, scale);
        var roundTrip = histogram.MapToIndex(lowerBoundary);
        Assert.Equal(maxIndex - 1, roundTrip);
    }

    [Theory]
    [MemberData(nameof(GetPositiveScales))]
    public void TestPositiveScalesLowerBoundaryMinIndex(int scale)
    {
        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var minIndex = histogram.MapToIndex(double.Epsilon);

        var lowerBoundary = Base2ExponentialBucketHistogram.LowerBoundary(minIndex, scale);
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
        var lowerBound = Base2ExponentialBucketHistogram.LowerBoundary(index, scale);
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
                preciseLowerBound = BitDecrement(preciseLowerBound);
                newRoundTrip = histogram.MapToIndex(preciseLowerBound);
                ++increments;
            }
        }
        else
        {
            for (; newRoundTrip < index;)
            {
                var newLowerBound = BitIncrement(preciseLowerBound);
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

    // Math.BitIncrement was introduced in .NET Core 3.0.
    // This is the implementation from:
    // https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib/src/System/Math.cs#L259
#pragma warning disable SA1204 // Static members should appear before non-static members
#pragma warning disable SA1119 // Statement should not use unnecessary parenthesis
    private static double BitIncrement(double x)
    {
#if NET6_0_OR_GREATER
        return Math.BitIncrement(x);
#else
        long bits = BitConverter.DoubleToInt64Bits(x);

        if (((bits >> 32) & 0x7FF00000) >= 0x7FF00000)
        {
            // NaN returns NaN
            // -Infinity returns double.MinValue
            // +Infinity returns +Infinity

            return (bits == unchecked((long)(0xFFF00000_00000000))) ? double.MinValue : x;
        }

        if (bits == unchecked((long)(0x80000000_00000000)))
        {
            // -0.0 returns double.Epsilon
            return double.Epsilon;
        }

        // Negative values need to be decremented
        // Positive values need to be incremented

        bits += ((bits < 0) ? -1 : +1);
        return BitConverter.Int64BitsToDouble(bits);
#endif
    }

    private static double BitDecrement(double x)
    {
#if NET6_0_OR_GREATER
        return Math.BitDecrement(x);
#else
        long bits = BitConverter.DoubleToInt64Bits(x);

        if (((bits >> 32) & 0x7FF00000) >= 0x7FF00000)
        {
            // NaN returns NaN
            // -Infinity returns -Infinity
            // +Infinity returns double.MaxValue
            return (bits == 0x7FF00000_00000000) ? double.MaxValue : x;
        }

        if (bits == 0x00000000_00000000)
        {
            // +0.0 returns -double.Epsilon
            return -double.Epsilon;
        }

        // Negative values need to be incremented
        // Positive values need to be decremented

        bits += ((bits < 0) ? +1 : -1);
        return BitConverter.Int64BitsToDouble(bits);
#endif
    }
#pragma warning restore SA1119 // Statement should not use unnecessary parenthesis
#pragma warning restore SA1204 // Static members should appear before non-static members
}
