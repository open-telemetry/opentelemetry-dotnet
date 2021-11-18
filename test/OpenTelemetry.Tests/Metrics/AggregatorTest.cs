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

            Assert.Equal(22, histogramPoint.LongValue);
            for (int i = 0; i < histogramPoint.BucketCounts.Length; i++)
            {
                Assert.Equal(2, histogramPoint.BucketCounts[i]);
            }
        }

        [Fact]
        public void HistogramDistributeToAllBucketsCustom()
        {
            var bounds = new double[] { 10, 20 };
            var histogramPoint = new MetricPoint(AggregationType.Histogram, DateTimeOffset.Now, null, null, bounds);

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

            // Sum of all recordings
            Assert.Equal(40, histogramPoint.DoubleValue);

            // Count  = # of recordings
            Assert.Equal(7, histogramPoint.LongValue);
            Assert.Equal(bounds.Length + 1, histogramPoint.BucketCounts.Length);
            Assert.Equal(5, histogramPoint.BucketCounts[0]);
            Assert.Equal(2, histogramPoint.BucketCounts[1]);
            Assert.Equal(0, histogramPoint.BucketCounts[2]);
        }

        [Fact]
        public void HistogramWithOnlySumCount()
        {
            var bounds = new double[] { };
            var histogramPoint = new MetricPoint(AggregationType.HistogramSumCount, DateTimeOffset.Now, null, null, bounds);

            histogramPoint.Update(-10);
            histogramPoint.Update(0);
            histogramPoint.Update(1);
            histogramPoint.Update(9);
            histogramPoint.Update(10);
            histogramPoint.Update(11);
            histogramPoint.Update(19);

            histogramPoint.TakeSnapshot(true);

            // Sum of all recordings
            Assert.Equal(40, histogramPoint.DoubleValue);

            // Count  = # of recordings
            Assert.Equal(7, histogramPoint.LongValue);
            Assert.Null(histogramPoint.BucketCounts);
            Assert.Null(histogramPoint.ExplicitBounds);
        }
    }
}
