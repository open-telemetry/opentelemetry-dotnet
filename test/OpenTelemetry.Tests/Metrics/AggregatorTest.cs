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

using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class AggregatorTest
    {
        private readonly AggregatorStore aggregatorStore = new("test", AggregationType.HistogramWithBuckets, AggregationTemporality.Cumulative, 1024, Metric.DefaultHistogramBounds, Metric.DefaultExponentialHistogramMaxBuckets);

        [Fact]
        public void HistogramDistributeToAllBucketsDefault()
        {
            var histogramPoint = new MetricPoint(this.aggregatorStore, AggregationType.HistogramWithBuckets, null, Metric.DefaultHistogramBounds, Metric.DefaultExponentialHistogramMaxBuckets);
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
            histogramPoint.Update(501);
            histogramPoint.Update(750);
            histogramPoint.Update(751);
            histogramPoint.Update(1000);
            histogramPoint.Update(1001);
            histogramPoint.Update(2500);
            histogramPoint.Update(2501);
            histogramPoint.Update(5000);
            histogramPoint.Update(5001);
            histogramPoint.Update(7500);
            histogramPoint.Update(7501);
            histogramPoint.Update(10000);
            histogramPoint.Update(10001);
            histogramPoint.Update(10000000);
            histogramPoint.TakeSnapshot(true);

            var count = histogramPoint.GetHistogramCount();

            Assert.Equal(32, count);

            int actualCount = 0;
            foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
            {
                Assert.Equal(2, histogramMeasurement.BucketCount);
                actualCount++;
            }
        }

        [Fact]
        public void HistogramDistributeToAllBucketsCustom()
        {
            var boundaries = new double[] { 10, 20 };
            var histogramPoint = new MetricPoint(this.aggregatorStore, AggregationType.HistogramWithBuckets, null, boundaries, Metric.DefaultExponentialHistogramMaxBuckets);

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

            // Sum of all recordings
            Assert.Equal(40, sum);

            // Count  = # of recordings
            Assert.Equal(7, count);

            int index = 0;
            int actualCount = 0;
            var expectedBucketCounts = new long[] { 5, 2, 0 };
            foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
            {
                Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
                index++;
                actualCount++;
            }

            Assert.Equal(boundaries.Length + 1, actualCount);
        }

        [Fact]
        public void HistogramBinaryBucketTest()
        {
            // Arrange
            // Bounds = (-Inf, 0] (0, 1], ... (49, +Inf)
            var boundaries = new double[HistogramBuckets.DefaultBoundaryCountForBinarySearch];
            for (var i = 0; i < boundaries.Length; i++)
            {
                boundaries[i] = i;
            }

            var histogramPoint = new MetricPoint(this.aggregatorStore, AggregationType.HistogramWithBuckets, null, boundaries, Metric.DefaultExponentialHistogramMaxBuckets);

            // Act
            histogramPoint.Update(-1);
            histogramPoint.Update(boundaries[0]);
            histogramPoint.Update(boundaries[boundaries.Length - 1]);
            for (var i = 0.5; i < boundaries.Length; i++)
            {
                histogramPoint.Update(i);
            }

            histogramPoint.TakeSnapshot(true);

            // Assert
            var index = 0;
            foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
            {
                var expectedCount = 1;

                if (index == 0 || index == boundaries.Length - 1)
                {
                    expectedCount = 2;
                }

                Assert.Equal(expectedCount, histogramMeasurement.BucketCount);
                index++;
            }
        }

        [Fact]
        public void HistogramWithOnlySumCount()
        {
            var boundaries = Array.Empty<double>();
            var histogramPoint = new MetricPoint(this.aggregatorStore, AggregationType.Histogram, null, boundaries, Metric.DefaultExponentialHistogramMaxBuckets);

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

            // There should be no enumeration of BucketCounts and ExplicitBounds for HistogramSumCount
            var enumerator = histogramPoint.GetHistogramBuckets().GetEnumerator();
            Assert.False(enumerator.MoveNext());
        }

        [Theory]
        [InlineData(AggregationType.Base2ExponentialHistogram, AggregationTemporality.Cumulative)]
        [InlineData(AggregationType.Base2ExponentialHistogram, AggregationTemporality.Delta)]
        [InlineData(AggregationType.Base2ExponentialHistogramWithMinMax, AggregationTemporality.Cumulative)]
        [InlineData(AggregationType.Base2ExponentialHistogramWithMinMax, AggregationTemporality.Delta)]
        internal void ExponentialHistogramTests(AggregationType aggregationType, AggregationTemporality aggregationTemporality)
        {
            var valuesToRecord = new[] { -10, 0, 1, 9, 10, 11, 19 };

            var aggregatorStore = new AggregatorStore(
                $"{nameof(this.ExponentialHistogramTests)}",
                aggregationType,
                aggregationTemporality,
                maxMetricPoints: 1024,
                Metric.DefaultHistogramBounds,
                Metric.DefaultExponentialHistogramMaxBuckets);

            var metricPoint = new MetricPoint(
                aggregatorStore,
                aggregationType, // TODO: Why is this here? AggregationType is already declared when AggregatorStore was instantiated.
                tagKeysAndValues: null,
                Metric.DefaultHistogramBounds,
                Metric.DefaultExponentialHistogramMaxBuckets);

            var expectedHistogram = new Base2ExponentialBucketHistogram();

            foreach (var value in valuesToRecord)
            {
                metricPoint.Update(value);
                expectedHistogram.Record(value);
            }

            metricPoint.TakeSnapshot(aggregationTemporality == AggregationTemporality.Delta); // TODO: Why outputDelta param? The aggregation temporality was declared when instantiateing the AggregatorStore.

            var count = metricPoint.GetHistogramCount();
            var sum = metricPoint.GetHistogramSum();
            var hasMinMax = metricPoint.TryGetHistogramMinMaxValues(out var min, out var max);

            AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint.GetExponentialHistogramData());
            Assert.Equal(40, sum);
            Assert.Equal(7, count);

            if (aggregationType == AggregationType.Base2ExponentialHistogramWithMinMax)
            {
                Assert.True(hasMinMax);
                Assert.Equal(-10, min);
                Assert.Equal(19, max);
            }
            else
            {
                Assert.False(hasMinMax);
            }

            metricPoint.TakeSnapshot(aggregationTemporality == AggregationTemporality.Delta);

            count = metricPoint.GetHistogramCount();
            sum = metricPoint.GetHistogramSum();
            hasMinMax = metricPoint.TryGetHistogramMinMaxValues(out min, out max);

            if (aggregationTemporality == AggregationTemporality.Cumulative)
            {
                AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint.GetExponentialHistogramData());
                Assert.Equal(40, sum);
                Assert.Equal(7, count);

                if (aggregationType == AggregationType.Base2ExponentialHistogramWithMinMax)
                {
                    Assert.True(hasMinMax);
                    Assert.Equal(-10, min);
                    Assert.Equal(19, max);
                }
                else
                {
                    Assert.False(hasMinMax);
                }
            }
            else
            {
                expectedHistogram.Reset();
                AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint.GetExponentialHistogramData());
                Assert.Equal(0, sum);
                Assert.Equal(0, count);

                if (aggregationType == AggregationType.Base2ExponentialHistogramWithMinMax)
                {
                    Assert.True(hasMinMax);
                    Assert.Equal(double.PositiveInfinity, min);
                    Assert.Equal(double.NegativeInfinity, max);
                }
                else
                {
                    Assert.False(hasMinMax);
                }
            }
        }

        private static void AssertExponentialBucketsAreCorrect(Base2ExponentialBucketHistogram expectedHistogram, ExponentialHistogramData data)
        {
            Assert.Equal(expectedHistogram.Scale, data.Scale);
            Assert.Equal(expectedHistogram.ZeroCount, data.ZeroCount);
            Assert.Equal(expectedHistogram.PositiveBuckets.Offset, data.PositiveBuckets.Offset);
            Assert.Equal(expectedHistogram.NegativeBuckets.Offset, data.NegativeBuckets.Offset);

            var index = expectedHistogram.PositiveBuckets.Offset;
            foreach (var bucketCount in data.PositiveBuckets)
            {
                Assert.Equal(expectedHistogram.PositiveBuckets[index++], bucketCount);
            }

            index = expectedHistogram.NegativeBuckets.Offset;
            foreach (var bucketCount in data.NegativeBuckets)
            {
                Assert.Equal(expectedHistogram.PositiveBuckets[index++], bucketCount);
            }
        }
    }
}
