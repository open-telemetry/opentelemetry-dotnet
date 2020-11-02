// <copyright file="ExplicitHistogramTest.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry.Metrics.Histogram;
using Xunit;
using Assert = Xunit.Assert;

namespace OpenTelemetry.Metrics.Tests
{
    public class ExplicitHistogramTest
    {
        [Fact]
        public void RecordValue_Success()
        {
            var explicitHistogram = new Int64ExplicitHistogram(new long[] { -1, 0, 1, 2, 4, 8, 10, 16 });
            var expectedBucketCounts = ImmutableArray.Create(new long[] { 1, 1, 1, 1, 2, 4, 2, 6, 2 });

            for (var i = -2; i <= 17; ++i)
            {
                explicitHistogram.RecordValue(i);
            }

            var distributionData = explicitHistogram.GetDistributionAndClear();
            CollectionAssert.AreEqual(expectedBucketCounts, distributionData.BucketCounts);
            Assert.Equal(20, distributionData.Count);
            Assert.Equal(7.5, distributionData.Mean);
            Assert.Equal(665, distributionData.SumOfSquaredDeviation);
        }

        [Fact]
        public void RecordValue_SuccessWithOneBoundary()
        {
            var explicitHistogram = new DoubleExplicitHistogram(new double[] { 0 });
            var expectedBucketCounts = ImmutableArray.Create(new long[] { 10, 11 });

            for (var i = -10; i <= 10; ++i)
            {
                explicitHistogram.RecordValue(i);
            }

            var distributionData = explicitHistogram.GetDistributionAndClear();
            CollectionAssert.AreEqual(expectedBucketCounts, distributionData.BucketCounts);
            Assert.Equal(21, distributionData.Count);
            Assert.Equal(0, distributionData.Mean);
            Assert.Equal(770, distributionData.SumOfSquaredDeviation);
        }

        [Fact]
        public void GetBucketCountsAndClear_ClearsCounts()
        {
            var explicitHistogram = new Int64ExplicitHistogram(new long[] { 0, 1 });
            var expectedBucketCounts = ImmutableArray.Create(new long[] { 0, 1, 1 });
            var expectedEmptyBucketCounts = ImmutableArray.Create(new long[] { 0, 0, 0 });

            explicitHistogram.RecordValue(0);
            explicitHistogram.RecordValue(1);

            var distributionData = explicitHistogram.GetDistributionAndClear();
            var clearDistributionData = explicitHistogram.GetDistributionAndClear();

            CollectionAssert.AreEqual(expectedBucketCounts, distributionData.BucketCounts);
            Assert.Equal(2, distributionData.Count);
            Assert.Equal(.5, distributionData.Mean);
            Assert.Equal(.5, distributionData.SumOfSquaredDeviation);
            CollectionAssert.AreEqual(expectedEmptyBucketCounts, clearDistributionData.BucketCounts);
            Assert.Equal(0, clearDistributionData.Count);
        }

        [Fact]
        public void ThrowsForInvalidHistogramParams()
        {
            try
            {
                var explicitHistogram = new Int64ExplicitHistogram(new long[] { });
                Assert.True(false, "Constructor should have thrown error.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            try
            {
                var explicitHistogram = new Int64ExplicitHistogram(new long[210]);
                Assert.True(false, "Constructor should have thrown error.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
    }
}
