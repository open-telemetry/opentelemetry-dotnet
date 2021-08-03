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
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class AggregatorTest
    {
        [Fact]
        public void HistogramDistributeToAllBuckets()
        {
            using var meter = new Meter("TestMeter", "0.0.1");

            var hist = new HistogramMetricAggregator("test", "desc", "1", meter, DateTimeOffset.UtcNow, new KeyValuePair<string, object>[0]);

            hist.Update<long>(-1);
            hist.Update<long>(0);
            hist.Update<long>(5);
            hist.Update<long>(10);
            hist.Update<long>(25);
            hist.Update<long>(50);
            hist.Update<long>(75);
            hist.Update<long>(100);
            hist.Update<long>(250);
            hist.Update<long>(500);
            hist.Update<long>(1000);
            var metric = hist.Collect(DateTimeOffset.UtcNow, false);

            Assert.NotNull(metric);
            Assert.IsType<HistogramMetricAggregator>(metric);

            if (metric is HistogramMetricAggregator agg)
            {
                int len = 0;
                foreach (var bucket in agg.Buckets)
                {
                    Assert.Equal(1, bucket.Count);
                    len++;
                }

                Assert.Equal(11, len);
            }
        }

        [Fact]
        public void HistogramCustomBoundaries()
        {
            using var meter = new Meter("TestMeter", "0.0.1");

            var hist = new HistogramMetricAggregator("test", "desc", "1", meter, DateTimeOffset.UtcNow, new KeyValuePair<string, object>[0], new double[] { 0 });

            hist.Update<long>(-1);
            hist.Update<long>(0);
            var metric = hist.Collect(DateTimeOffset.UtcNow, false);

            Assert.NotNull(metric);
            Assert.IsType<HistogramMetricAggregator>(metric);

            if (metric is HistogramMetricAggregator agg)
            {
                int len = 0;
                foreach (var bucket in agg.Buckets)
                {
                    Assert.Equal(1, bucket.Count);
                    len++;
                }

                Assert.Equal(2, len);
            }
        }

        [Fact]
        public void HistogramWithEmptyBuckets()
        {
            using var meter = new Meter("TestMeter", "0.0.1");

            var hist = new HistogramMetricAggregator("test", "desc", "1", meter, DateTimeOffset.UtcNow, new KeyValuePair<string, object>[0], new double[] { 0, 5, 10 });

            hist.Update<long>(-3);
            hist.Update<long>(-2);
            hist.Update<long>(-1);
            hist.Update<long>(6);
            hist.Update<long>(7);
            hist.Update<long>(12);
            var metric = hist.Collect(DateTimeOffset.UtcNow, false);

            Assert.NotNull(metric);
            Assert.IsType<HistogramMetricAggregator>(metric);

            if (metric is HistogramMetricAggregator agg)
            {
                var expectedCounts = new int[] { 3, 0, 2, 1 };
                int len = 0;
                foreach (var bucket in agg.Buckets)
                {
                    if (len < expectedCounts.Length)
                    {
                        Assert.Equal(expectedCounts[len], bucket.Count);
                        len++;
                    }
                }

                Assert.Equal(4, len);
            }
        }
    }
}
