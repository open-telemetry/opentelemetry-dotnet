// <copyright file="ExponentialBucketHistogramTest.cs" company="OpenTelemetry Authors">
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

using System;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class ExponentialBucketHistogramTest
    {
        [Fact]
        public void IndexLookup()
        {
            // An exponential bucket histogram with scale = 0.
            // The base is 2 ^ (2 ^ -0) = 2.
            // The buckets are:
            //
            // ...
            // bucket[-3]: (1/8, 1/4]
            // bucket[-2]: (1/4, 1/2]
            // bucket[-1]: (1/2, 1]
            // bucket[0]:  (1, 2]
            // bucket[1]:  (2, 4]
            // bucket[2]:  (4, 8]
            // bucket[3]:  (8, 16]
            // ...

            var histogram_scale0 = new ExponentialBucketHistogram(0);

            Assert.Equal(-1075, histogram_scale0.MapToIndex(double.Epsilon));

            Assert.Equal(-1074, histogram_scale0.MapToIndex(double.Epsilon * 2));

            Assert.Equal(-1073, histogram_scale0.MapToIndex(double.Epsilon * 3));
            Assert.Equal(-1073, histogram_scale0.MapToIndex(double.Epsilon * 4));

            Assert.Equal(-1072, histogram_scale0.MapToIndex(double.Epsilon * 5));
            Assert.Equal(-1072, histogram_scale0.MapToIndex(double.Epsilon * 6));
            Assert.Equal(-1072, histogram_scale0.MapToIndex(double.Epsilon * 7));
            Assert.Equal(-1072, histogram_scale0.MapToIndex(double.Epsilon * 8));

            Assert.Equal(-1023, histogram_scale0.MapToIndex(2.2250738585072009E-308));
            Assert.Equal(-1023, histogram_scale0.MapToIndex(2.2250738585072014E-308));

            Assert.Equal(-3, histogram_scale0.MapToIndex(0.25));

            Assert.Equal(-2, histogram_scale0.MapToIndex(0.375));
            Assert.Equal(-2, histogram_scale0.MapToIndex(0.5));

            Assert.Equal(-1, histogram_scale0.MapToIndex(0.75));
            Assert.Equal(-1, histogram_scale0.MapToIndex(1));

            Assert.Equal(0, histogram_scale0.MapToIndex(1.5));
            Assert.Equal(0, histogram_scale0.MapToIndex(2));

            Assert.Equal(1, histogram_scale0.MapToIndex(3));
            Assert.Equal(1, histogram_scale0.MapToIndex(4));

            Assert.Equal(2, histogram_scale0.MapToIndex(5));
            Assert.Equal(2, histogram_scale0.MapToIndex(6));
            Assert.Equal(2, histogram_scale0.MapToIndex(7));
            Assert.Equal(2, histogram_scale0.MapToIndex(8));

            Assert.Equal(3, histogram_scale0.MapToIndex(9));
            Assert.Equal(3, histogram_scale0.MapToIndex(16));

            Assert.Equal(4, histogram_scale0.MapToIndex(17));
            Assert.Equal(4, histogram_scale0.MapToIndex(32));

            // An exponential bucket histogram with scale = 1.
            // The base is 2 ^ (2 ^ -1) = sqrt(2) = 1.41421356237.
            // The buckets are:
            //
            // ...
            // bucket[-3]: (0.35355339059, 1/2]
            // bucket[-2]: (1/2, 0.70710678118]
            // bucket[-1]: (0.70710678118, 1]
            // bucket[0]:  (1, 1.41421356237]
            // bucket[1]:  (1.41421356237, 2]
            // bucket[2]:  (2, 2.82842712474]
            // bucket[3]:  (2.82842712474, 4]
            // ...

            var histogram_scale1 = new ExponentialBucketHistogram(1);

            Assert.Equal(-3, histogram_scale1.MapToIndex(0.5));

            Assert.Equal(-2, histogram_scale1.MapToIndex(0.6));

            Assert.Equal(-1, histogram_scale1.MapToIndex(1));

            Assert.Equal(1, histogram_scale1.MapToIndex(2));

            Assert.Equal(3, histogram_scale1.MapToIndex(4));
        }
    }
}

#endif
