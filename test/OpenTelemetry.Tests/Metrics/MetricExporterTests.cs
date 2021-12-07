// <copyright file="MetricExporterTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricExporterTests
    {
        [Theory]
        [InlineData(ExportModes.Push)]
        [InlineData(ExportModes.Pull)]
        [InlineData(ExportModes.Pull | ExportModes.Push)]
        public void FlushMetricExporterTest(ExportModes mode)
        {
            BaseExporter<Metric> exporter = null;

            switch (mode)
            {
                case ExportModes.Push:
                    exporter = new PushOnlyMetricExporter();
                    break;
                case ExportModes.Pull:
                    exporter = new PullOnlyMetricExporter();
                    break;
                case ExportModes.Pull | ExportModes.Push:
                    exporter = new PushPullMetricExporter();
                    break;
            }

            var reader = new BaseExportingMetricReader(exporter);
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddReader(reader)
                .Build();

            switch (mode)
            {
                case ExportModes.Push:
                    Assert.True(reader.Collect());
                    Assert.True(meterProvider.ForceFlush());
                    break;
                case ExportModes.Pull:
                    Assert.False(reader.Collect());
                    Assert.False(meterProvider.ForceFlush());
                    Assert.True((exporter as IPullMetricExporter).Collect(-1));
                    break;
                case ExportModes.Pull | ExportModes.Push:
                    Assert.True(reader.Collect());
                    Assert.True(meterProvider.ForceFlush());
                    break;
            }
        }

        [Fact]
        public void ExporterShouldNotUpdateMetricPoint()
        {
            var exportedItems = new List<Metric>();
            var updateMetricPointExporter = new UpdateMetricPointExporter(exportedItems);
            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(new BaseExportingMetricReader(updateMetricPointExporter) { Temporality = AggregationTemporality.Delta })
            .Build();

            var counter = meter.CreateCounter<long>("counter");
            counter.Add(100, new KeyValuePair<string, object>("key", "value"));

            meterProvider.ForceFlush();

            Assert.Single(exportedItems);
            var metricPoint = this.GetFirstMetricPointFromMetric(exportedItems[0]);
            Assert.Equal(100, metricPoint.GetSumLong());
            foreach (var tag in metricPoint.Tags)
            {
                Assert.Equal("key", tag.Key);
                Assert.Equal("value", tag.Value);
            }

            exportedItems.Clear();

            counter.Add(150, new KeyValuePair<string, object>("key", "value"));

            meterProvider.ForceFlush();

            Assert.Single(exportedItems);
            metricPoint = this.GetFirstMetricPointFromMetric(exportedItems[0]);
            Assert.Equal(150, metricPoint.GetSumLong());
            foreach (var tag in metricPoint.Tags)
            {
                Assert.Equal("key", tag.Key);
                Assert.Equal("value", tag.Value);
            }
        }

        private ref MetricPoint GetFirstMetricPointFromMetric(Metric metric)
        {
            var metricPoints = metric.GetMetricPoints();
            var metricPointsEnumerator = metricPoints.GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
            return ref metricPointsEnumerator.Current;
        }

        [ExportModes(ExportModes.Push)]
        private class PushOnlyMetricExporter : BaseExporter<Metric>
        {
            public override ExportResult Export(in Batch<Metric> batch)
            {
                return ExportResult.Success;
            }
        }

        [ExportModes(ExportModes.Pull)]
        private class PullOnlyMetricExporter : BaseExporter<Metric>, IPullMetricExporter
        {
            private Func<int, bool> funcCollect;

            public Func<int, bool> Collect
            {
                get => this.funcCollect;
                set { this.funcCollect = value; }
            }

            public override ExportResult Export(in Batch<Metric> batch)
            {
                return ExportResult.Success;
            }
        }

        [ExportModes(ExportModes.Pull | ExportModes.Push)]
        private class PushPullMetricExporter : BaseExporter<Metric>
        {
            public override ExportResult Export(in Batch<Metric> batch)
            {
                return ExportResult.Success;
            }
        }

        private class UpdateMetricPointExporter : BaseExporter<Metric>
        {
            private static int invocationCount = 1;
            private readonly List<Metric> exportedItems;

            public UpdateMetricPointExporter(List<Metric> exportedItems)
            {
                this.exportedItems = exportedItems;
            }

            public override ExportResult Export(in Batch<Metric> batch)
            {
                foreach (var metric in batch)
                {
                    if (invocationCount > 1)
                    {
                        foreach (ref var metricPoint in metric.GetMetricPoints())
                        {
                            metricPoint = default;
                        }
                    }

                    this.exportedItems.Add(metric);
                }

                invocationCount++;
                return ExportResult.Success;
            }
        }
    }
}
