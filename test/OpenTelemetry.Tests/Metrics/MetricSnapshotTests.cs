// <copyright file="MetricSnapshotTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Metrics;

using OpenTelemetry.Tests;

using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricSnapshotTests
    {
        [Fact]
        public void VerifySnapshot_Counter()
        {
            var exportedMetrics = new List<Metric>();
            var exportedSnapshots = new List<MetricSnapshot>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedMetrics)
                .AddInMemoryExporter(exportedSnapshots)
                .Build();

            var counter = meter.CreateCounter<long>("meter");
            counter.Add(10);

            meterProvider.ForceFlush();

            // Verify Metric
            Assert.Single(exportedMetrics);
            var metric1 = exportedMetrics[0];
            var metricPointsEnumerator = metric1.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext());
            ref readonly var metricPointForFirstExport = ref metricPointsEnumerator.Current;
            Assert.Equal(10, metricPointForFirstExport.GetSumLong());

            // Verify Snapshot
            Assert.Single(exportedSnapshots);
            var snapshot1 = exportedSnapshots[0];
            Assert.Single(snapshot1.MetricPoints);
            Assert.Equal(10, snapshot1.MetricPoints[0].GetSumLong());

            // Verify Metric == Snapshot
            Assert.Equal(metric1.Name, snapshot1.Name);
            Assert.Equal(metric1.Description, snapshot1.Description);
            Assert.Equal(metric1.Unit, snapshot1.Unit);
            Assert.Equal(metric1.MeterName, snapshot1.MeterName);
            Assert.Equal(metric1.MetricType, snapshot1.MetricType);
            Assert.Equal(metric1.MeterVersion, snapshot1.MeterVersion);
        }

        [Fact]
        public void VerifySnapshot_Histogram()
        {
            var exportedMetrics = new List<Metric>();
            var exportedSnapshots = new List<MetricSnapshot>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedMetrics)
                .AddInMemoryExporter(exportedSnapshots)
                .Build();

            var histogram = meter.CreateHistogram<int>("histogram");
            histogram.Record(10);

            meterProvider.ForceFlush();

            // Verify Metric
            Assert.Single(exportedMetrics);
            var metric1 = exportedMetrics[0];
            var metricPointsEnumerator = metric1.GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext());
            ref readonly var metricPointForFirstExport = ref metricPointsEnumerator.Current;
            Assert.Equal(1, metricPointForFirstExport.GetHistogramCount());
            Assert.Equal(10, metricPointForFirstExport.GetHistogramSum());

            // Verify Snapshot
            Assert.Single(exportedSnapshots);
            var snapshot1 = exportedSnapshots[0];
            Assert.Single(snapshot1.MetricPoints);
            Assert.Equal(1, snapshot1.MetricPoints[0].GetHistogramCount());
            Assert.Equal(10, snapshot1.MetricPoints[0].GetHistogramSum());

            // Verify Metric == Snapshot
            Assert.Equal(metric1.Name, snapshot1.Name);
            Assert.Equal(metric1.Description, snapshot1.Description);
            Assert.Equal(metric1.Unit, snapshot1.Unit);
            Assert.Equal(metric1.MeterName, snapshot1.MeterName);
            Assert.Equal(metric1.MetricType, snapshot1.MetricType);
            Assert.Equal(metric1.MeterVersion, snapshot1.MeterVersion);
        }
    }
}
