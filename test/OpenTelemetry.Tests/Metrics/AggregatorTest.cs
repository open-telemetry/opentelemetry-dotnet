// <copyright file="AggregatorTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class AggregatorTest
    {
        [Fact]
        public void HistogramDistributeToAllBucketsDefault()
        {
            var histogramPoint = new MetricPoint(AggregationType.Histogram, DateTimeOffset.Now, null, null, Metric.DefaultHistogramBounds);
            histogramPoint.Update(-1);
            histogramPoint.Update(0);
            histogramPoint.Update(2);
            histogramPoint.Update(5);
            histogramPoint.Update(8);
            histogramPoint.Update(10);
            histogramPoint.Update(11);
            histogramPoint.Update(25);
            histogramPoint.Update(40);
            histogramPoint.Update(50);
            histogramPoint.Update(70);
            histogramPoint.Update(75);
            histogramPoint.Update(99);
            histogramPoint.Update(100);
            histogramPoint.Update(246);
            histogramPoint.Update(250);
            histogramPoint.Update(499);
            histogramPoint.Update(500);
            histogramPoint.Update(999);
            histogramPoint.Update(1000);
            histogramPoint.Update(1001);
            histogramPoint.Update(10000000);
            histogramPoint.TakeSnapshot(true);

            var count = histogramPoint.GetHistogramCount();
            var bucketCounts = histogramPoint.GetBucketCounts();

            Assert.Equal(22, count);
            for (int i = 0; i < bucketCounts.Length; i++)
            {
                Assert.Equal(2, bucketCounts[i]);
            }
        }

        [Fact]
        public void HistogramDistributeToAllBucketsCustom()
        {
            var boundaries = new double[] { 10, 20 };
            var histogramPoint = new MetricPoint(AggregationType.Histogram, DateTimeOffset.Now, null, null, boundaries);

            // 5 recordings <=10
            histogramPoint.Update(-10);
            histogramPoint.Update(0);
            histogramPoint.Update(1);
            histogramPoint.Update(9);
            histogramPoint.Update(10);

            // 2 recordings >10, <=20
            histogramPoint.Update(11);
            histogramPoint.Update(19);

            histogramPoint.TakeSnapshot(true);

            var count = histogramPoint.GetHistogramCount();
            var sum = histogramPoint.GetHistogramSum();
            var bucketCounts = histogramPoint.GetBucketCounts();

            // Sum of all recordings
            Assert.Equal(40, sum);

            // Count  = # of recordings
            Assert.Equal(7, count);
            Assert.Equal(boundaries.Length + 1, bucketCounts.Length);
            Assert.Equal(5, bucketCounts[0]);
            Assert.Equal(2, bucketCounts[1]);
            Assert.Equal(0, bucketCounts[2]);
        }

        [Fact]
        public void HistogramWithOnlySumCount()
        {
            var boundaries = new double[] { };
            var histogramPoint = new MetricPoint(AggregationType.HistogramSumCount, DateTimeOffset.Now, null, null, boundaries);

            histogramPoint.Update(-10);
            histogramPoint.Update(0);
            histogramPoint.Update(1);
            histogramPoint.Update(9);
            histogramPoint.Update(10);
            histogramPoint.Update(11);
            histogramPoint.Update(19);

            histogramPoint.TakeSnapshot(true);

            var count = histogramPoint.GetHistogramCount();
            var sum = histogramPoint.GetHistogramSum();

            // Sum of all recordings
            Assert.Equal(40, sum);

            // Count  = # of recordings
            Assert.Equal(7, count);

            Assert.Throws<InvalidOperationException>(() => histogramPoint.GetBucketCounts());
            Assert.Throws<InvalidOperationException>(() => histogramPoint.GetExplicitBounds());
        }
    }
}
