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

#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
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

    public static IEnumerable<object[]> TestScales => new List<object[]>
    {
        new object[] { -11 },
        new object[] { -10 },
        new object[] { -9 },
        new object[] { -8 },
        new object[] { -7 },
        new object[] { -6 },
        new object[] { -5 },
        new object[] { -4 },
        new object[] { -3 },
        new object[] { -2 },
        new object[] { -1 },
        new object[] { 0 },
        new object[] { 1 },
        new object[] { 2 },
        new object[] { 3 },
        new object[] { 4 },
        new object[] { 5 },
        new object[] { 6 },
        new object[] { 7 },
        new object[] { 8 },
        new object[] { 9 },
        new object[] { 10 },
        new object[] { 11 },
        new object[] { 12 },
        new object[] { 13 },
        new object[] { 14 },
        new object[] { 15 },
        new object[] { 16 },
        new object[] { 17 },
        new object[] { 18 },
        new object[] { 19 },
        new object[] { 20 },
    };

    [Theory]
    [MemberData(nameof(TestScales))]
    public void LowerBoundaryPowersOfTwoRoundTripTest(int scale)
    {
        var histogram = new Base2ExponentialBucketHistogram(scale: scale);
        var indexesPerPowerOf2 = scale > 0 ? 1 << scale : 1;
        var minIndex = histogram.MapToIndex(double.Epsilon);
        var maxIndex = histogram.MapToIndex(double.MaxValue);

        // Check indexes >= 0
        for (var index = 0; index <= maxIndex; index += indexesPerPowerOf2)
        {
            var lowerBound = Base2ExponentialHistogramHelper.LowerBoundary(index, scale);
            var roundTrip = histogram.MapToIndex(lowerBound);
            Assert.Equal(index, roundTrip + 1);

            var match = false;
            for (var offset = 1; offset <= 1127; ++offset)
            {
                var lowerBoundDelta = lowerBound;
                for (var j = 0; j <= offset; ++j)
                {
                    lowerBoundDelta = BitIncrement(lowerBoundDelta);
                }

                roundTrip = histogram.MapToIndex(lowerBoundDelta);
                if (index == roundTrip)
                {
                    // var delta = lowerBoundDelta - lowerBound;
                    // output.WriteLine($"Scale={scale}, Ops={offset}, Index={index}, Delta={delta}");
                    match = true;
                    break;
                }
            }

            Assert.True(match);
        }

        // Check indexes < 0
        for (var index = minIndex; index < 0; index += indexesPerPowerOf2)
        {
            var lowerBound = Base2ExponentialHistogramHelper.LowerBoundary(index, scale);

            if (scale <= 0)
            {
                // TODO: For scales <= 0, LowerBoundary returns 0 instead of double.Epsilon for the minimum bucket index.
                // Should LowerBoundary just return double.Epsilon in this case?
                lowerBound = index == minIndex && lowerBound == 0
                    ? double.Epsilon

                    // TODO: All negative scales except -11 require this adjustment. Why?
                    : (scale != -11 ? BitIncrement(lowerBound) : lowerBound);
            }

            var isX64 = true;
#if NET6_0_OR_GREATER
            isX64 = RuntimeInformation.ProcessArchitecture == Architecture.X64;
#endif

            // TODO: This is not required on M1 Mac (ARM64)
            if ((scale > 0 && index == minIndex && lowerBound == 0 && isX64)
                || (scale == 1 && index <= minIndex + 2 && lowerBound == 0 && isX64))
            {
                lowerBound = double.Epsilon;
            }

            if (lowerBound == 0)
            {
                this.output.WriteLine($"{index}");
            }

            var roundTrip = histogram.MapToIndex(lowerBound);

            if (scale > 0)
            {
                if (index != roundTrip)
                {
                    int offset = 1;
                    for (var i = 0; offset <= 512; offset = 1 << ++i)
                    {
                        var lowerBoundDelta = lowerBound;
                        for (var j = 1; j <= offset; ++j)
                        {
                            lowerBoundDelta = BitIncrement(lowerBoundDelta);
                        }

                        var newRoundTrip = histogram.MapToIndex(lowerBoundDelta);

                        // Check offset + 1
                        if (index != newRoundTrip)
                        {
                            // offset++;
                            lowerBoundDelta = BitIncrement(lowerBoundDelta);
                            newRoundTrip = histogram.MapToIndex(lowerBoundDelta);
                        }

                        if (index == newRoundTrip)
                        {
                            // var delta = lowerBoundDelta - lowerBound;
                            // output.WriteLine($"Scale={scale}, Ops={offset}, Index={index}, Delta={delta}");
                            roundTrip = newRoundTrip;
                            break;
                        }
                    }
                }
            }

            Assert.Equal(index, roundTrip);
        }
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
#pragma warning restore SA1119 // Statement should not use unnecessary parenthesis
}
