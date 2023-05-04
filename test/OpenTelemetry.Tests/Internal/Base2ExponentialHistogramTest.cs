// <copyright file="Base2ExponentialHistogramTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Tests.Internal;

public class Base2ExponentialHistogramTest
{
    private readonly ITestOutputHelper output;

    public Base2ExponentialHistogramTest(ITestOutputHelper output)
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
            var lowerBound = Base2ExponentialHistogramHelper.LowerBoundary(index, scale);

            if (scale != 11 && index > minIndex && index < 0)
            {
                // TODO: All negative scales except -11 require this adjustment. Why?
                lowerBound = BitIncrement(lowerBound);
            }

            var roundTrip = histogram.MapToIndex(lowerBound);

            if (index >= 0)
            {
                Assert.Equal(index - 1, roundTrip);
                roundTrip = histogram.MapToIndex(BitIncrement(lowerBound));
                Assert.Equal(index, roundTrip);
            }
            else
            {
                Assert.Equal(index, roundTrip);
                if (lowerBound != double.Epsilon)
                {
                    roundTrip = histogram.MapToIndex(BitDecrement(lowerBound));
                    Assert.Equal(index - 1, roundTrip);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetPositiveScales))]
    public void TestPositiveScalesLowerBoundaryRoundTripPowersOfTwo(int scale)
    {
        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var minIndex = histogram.MapToIndex(double.Epsilon);
        var maxIndex = histogram.MapToIndex(double.MaxValue);

        var indexesPerPowerOf2 = 1 << scale;

        double maxDiff = 0;
        double maxOps = 0;

        for (var index = -indexesPerPowerOf2; index > minIndex; index -= indexesPerPowerOf2)
        {
            var lowerBound = Base2ExponentialHistogramHelper.LowerBoundary(index, scale);

            // Not necessary on M1 Mac
            if (lowerBound == 0)
            {
                lowerBound = double.Epsilon;
            }

            var roundTrip = histogram.MapToIndex(lowerBound);
            if (index == roundTrip)
            {
                var lowerBoundDelta = lowerBound;
                var newRoundTrip = roundTrip;
                var diff = 0;
                while (newRoundTrip != index - 1)
                {
                    lowerBoundDelta = BitDecrement(lowerBoundDelta);
                    newRoundTrip = histogram.MapToIndex(lowerBoundDelta);
                    ++diff;
                }

                Assert.Equal(index - 1, newRoundTrip);
                maxDiff = Math.Max(maxDiff, lowerBound - lowerBoundDelta);
                maxOps = Math.Max(maxOps, diff);
            }
            else
            {
                Assert.Equal(index - 1, roundTrip);

                var lowerBoundDelta = lowerBound;
                var newRoundTrip = roundTrip;
                var diff = 0;
                while (newRoundTrip < index)
                {
                    lowerBoundDelta = BitIncrement(lowerBoundDelta);
                    newRoundTrip = histogram.MapToIndex(lowerBoundDelta);
                    ++diff;
                }

                // It is possible for an index to be skipped, so we do not do an equal check.
                // Assert.Equal(index, newRoundTrip);
                Assert.True(index <= newRoundTrip);
                maxDiff = Math.Max(maxDiff, lowerBoundDelta - lowerBound);
                maxOps = Math.Max(maxOps, diff);
            }
        }

        for (var index = indexesPerPowerOf2; index < maxIndex; index += indexesPerPowerOf2)
        {
            var lowerBound = Base2ExponentialHistogramHelper.LowerBoundary(index, scale);
            var roundTrip = histogram.MapToIndex(lowerBound);

            Assert.Equal(index - 1, roundTrip);

            var lowerBoundDelta = lowerBound;
            var newRoundTrip = roundTrip;
            var diff = 0;
            while (newRoundTrip < index)
            {
                lowerBoundDelta = BitIncrement(lowerBoundDelta);
                newRoundTrip = histogram.MapToIndex(lowerBoundDelta);
                ++diff;
            }

            Assert.Equal(index, newRoundTrip);
            maxDiff = Math.Max(maxDiff, lowerBoundDelta - lowerBound);
            maxOps = Math.Max(maxOps, diff);
        }

        this.output.WriteLine($"maxDiff = {maxDiff}, maxOps = {maxOps}");
    }

    // Math.BitIncrement was introduced in .NET Core 3.0.
    // This is the implementation from:
    // https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib/src/System/Math.cs#L259
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
}
