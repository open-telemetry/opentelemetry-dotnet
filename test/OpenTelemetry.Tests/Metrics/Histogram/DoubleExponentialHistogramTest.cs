// <copyright file="DoubleExponentialHistogramTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry.Metrics.Histogram;
using Xunit;
using Assert = Xunit.Assert;

namespace OpenTelemetry.Metrics.Tests
{
    public class DoubleExponentialHistogramTest
    {
        [Fact]
        public void RecordValue()
        {
            // expected bucket boundaries: { .5, 1.5, 4.5, 13.75, 40.5, 121.5 }
            var exponentialHistogram = new DoubleExponentialHistogram(.5, 3, 5);
            var expectedBucketCounts = ImmutableArray.Create(new long[] { 1, 2, 1, 2, 1, 2, 2 });

            exponentialHistogram.RecordValue(0);
            exponentialHistogram.RecordValue(.5);
            exponentialHistogram.RecordValue(1);
            exponentialHistogram.RecordValue(1.5);
            exponentialHistogram.RecordValue(4.5);
            exponentialHistogram.RecordValue(5);
            exponentialHistogram.RecordValue(13.75);
            exponentialHistogram.RecordValue(40.5);
            exponentialHistogram.RecordValue(41);
            exponentialHistogram.RecordValue(121.5);
            exponentialHistogram.RecordValue(121.6);

            var distributionData = exponentialHistogram.GetDistributionAndClear();
            CollectionAssert.AreEqual(expectedBucketCounts, distributionData.BucketCounts);
            Assert.Equal(11, distributionData.Count);
            Assert.Equal(31.895, Math.Round(distributionData.Mean, 3));
            Assert.Equal(21917.352, Math.Round(distributionData.SumOfSquaredDeviation, 3));
        }
    }
}
