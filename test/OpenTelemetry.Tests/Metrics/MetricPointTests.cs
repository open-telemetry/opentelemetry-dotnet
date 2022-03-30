// <copyright file="MetricPointTests.cs" company="OpenTelemetry Authors">
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
// </copyright>;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricPointTests : IDisposable
    {
        private Meter meter;
        private MeterProvider provider;
        private Metric metric;
        private MetricPoint metricPoint;

        private Histogram<long> histogram;
        private double[] bounds;

        public MetricPointTests()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.histogram = this.meter.CreateHistogram<long>("histogram");

            // Evenly distribute the bound values over the range [0, MaxValue)
            this.bounds = new double[10];
            for (int i = 0; i < this.bounds.Length; i++)
            {
                this.bounds[i] = i * 1000 / this.bounds.Length;
            }

            var exportedItems = new List<Metric>();

            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddInMemoryExporter(exportedItems)
                .AddView(this.histogram.Name, new ExplicitBucketHistogramConfiguration() { Boundaries = this.bounds })
                .Build();

            this.histogram.Record(500);

            this.provider.ForceFlush();

            this.metric = exportedItems[0];
            var metricPointsEnumerator = this.metric.GetMetricPoints().GetEnumerator();
            metricPointsEnumerator.MoveNext();
            this.metricPoint = metricPointsEnumerator.Current;
        }

        public void Dispose()
        {
            this.meter?.Dispose();
            this.provider?.Dispose();
        }

        [Fact]
        public void VerifyMetricPointCopy()
        {
            var copy = this.metricPoint.Copy();

            // Verify these structs are unique instances.
            Assert.NotEqual(copy, this.metricPoint);

            // Verify properties are copied.
            Assert.Equal(copy.Tags, this.metricPoint.Tags);
            Assert.Equal(copy.StartTime, this.metricPoint.StartTime);
            Assert.Equal(copy.EndTime, this.metricPoint.EndTime);
        }

        [Fact]
        public void VerifyHistogramBucketsCopy()
        {
            var histogramBuckets = this.metricPoint.GetHistogramBuckets();
            var copy = histogramBuckets.Copy();

            // Verify these are unique instances.
            Assert.NotSame(copy, histogramBuckets);

            // Verify fields are copied
            Assert.Equal(copy.SnapshotBucketCounts, histogramBuckets.SnapshotBucketCounts);
            Assert.Equal(copy.SnapshotSum, histogramBuckets.SnapshotSum);
        }
    }
}
