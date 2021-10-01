// <copyright file="MetricViewTests.cs" company="OpenTelemetry Authors">
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
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricViewTests
    {
        private const int MaxTimeToAllowForFlush = 10000;
        private readonly ITestOutputHelper output;

        public MetricViewTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ViewToRenameMetric()
        {
            using var meter1 = new Meter("ViewToRenameMetricTest");
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource(meter1.Name)
                .AddView("name1", "renamed")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("renamed", metric.Name);
        }

        [Fact]
        public void ViewToRenameMetricConditionally()
        {
            using var meter1 = new Meter("ViewToRenameMetricConditionallyTest");
            using var meter2 = new Meter("ViewToRenameMetricConditionallyTest2");
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource(meter1.Name)
                .AddSource(meter2.Name)
                .AddView((instrument) =>
                {
                    if (instrument.Meter.Name.Equals(meter2.Name, StringComparison.OrdinalIgnoreCase)
                        && instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                    {
                        return new MetricStreamConfiguration() { Name = "name1_Renamed", Description = "new description" };
                    }
                    else
                    {
                        return null;
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Without views only 1 stream would be
            // exported (the 2nd one gets dropped due to
            // name conflict). Due to renaming with Views,
            // we expect 2 metric streams here.
            var counter1 = meter1.CreateCounter<long>("name1", "unit", "original_description");
            var counter2 = meter2.CreateCounter<long>("name1", "unit", "original_description");
            counter1.Add(10);
            counter2.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            Assert.Equal("name1", exportedItems[0].Name);
            Assert.Equal("name1_Renamed", exportedItems[1].Name);
            Assert.Equal("original_description", exportedItems[0].Description);
            Assert.Equal("new description", exportedItems[1].Description);
        }

        [Fact]
        public void ViewToRenameMetricWildCardMatch()
        {
            using var meter1 = new Meter("ViewToRenameMetricWildCardMatchTest");
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource(meter1.Name)
                .AddView("counter*", "renamed")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            var counter1 = meter1.CreateCounter<long>("counterA");
            counter1.Add(10);
            var counter2 = meter1.CreateCounter<long>("counterB");
            counter2.Add(10);
            var counter3 = meter1.CreateCounter<long>("counterC");
            counter3.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // counter* matches all 3 instruments which all
            // becomes "renamed" and only 1st one is exported.
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("renamed", metric.Name);
        }

        [Fact]
        public void ViewToProduceMultipleStreamsFromInstrument()
        {
            using var meter1 = new Meter("ViewToProduceMultipleStreamsFromInstrumentTest");
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource(meter1.Name)
                .AddView("name1", "renamedStream1")
                .AddView("name1", "renamedStream2")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting two metric stream.
            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            Assert.Equal("renamedStream1", exportedItems[0].Name);
            Assert.Equal("renamedStream2", exportedItems[1].Name);
        }

        [Fact]
        public void ViewToProduceMultipleStreamsWithDuplicatesFromInstrument()
        {
            using var meter1 = new Meter("ViewToProduceMultipleStreamsWithDuplicatesFromInstrumentTest");
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource(meter1.Name)
                .AddView("name1", "renamedStream1")
                .AddView("name1", "renamedStream2")
                .AddView("name1", "renamedStream2")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting two metric stream.
            // the .AddView("name1", "renamedStream2")
            // won't produce new Metric as the name
            // conflicts.
            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            Assert.Equal("renamedStream1", exportedItems[0].Name);
            Assert.Equal("renamedStream2", exportedItems[1].Name);
        }

        [Fact]
        public void ViewToProduceCustomHistogramBound()
        {
            using var meter1 = new Meter("ViewToProduceCustomHistogramBoundTest");
            var exportedItems = new List<Metric>();
            var bounds = new double[] { 10, 20 };
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource(meter1.Name)
                .AddView("MyHistogram", new HistogramConfiguration() { Name = "MyHistogramDefaultBound" })
                .AddView("MyHistogram", new HistogramConfiguration() { BucketBounds = bounds })
                .AddInMemoryExporter(exportedItems)
                .Build();

            var histogram = meter1.CreateHistogram<long>("MyHistogram");
            histogram.Record(-10);
            histogram.Record(0);
            histogram.Record(1);
            histogram.Record(9);
            histogram.Record(10);
            histogram.Record(11);
            histogram.Record(19);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            var metricDefault = exportedItems[0];
            var metricCustom = exportedItems[1];

            Assert.Equal("MyHistogramDefaultBound", metricDefault.Name);
            Assert.Equal("MyHistogram", metricCustom.Name);

            List<MetricPoint> metricPointsDefault = new List<MetricPoint>();
            foreach (ref var mp in metricDefault.GetMetricPoints())
            {
                metricPointsDefault.Add(mp);
            }

            Assert.Single(metricPointsDefault);
            var histogramPoint = metricPointsDefault[0];

            Assert.Equal(40, histogramPoint.DoubleValue);
            Assert.Equal(7, histogramPoint.LongValue);
            Assert.Equal(Metric.DefaultHistogramBounds.Length + 1, histogramPoint.BucketCounts.Length);
            Assert.Equal(2, histogramPoint.BucketCounts[0]);
            Assert.Equal(1, histogramPoint.BucketCounts[1]);
            Assert.Equal(2, histogramPoint.BucketCounts[2]);
            Assert.Equal(2, histogramPoint.BucketCounts[3]);
            Assert.Equal(0, histogramPoint.BucketCounts[4]);
            Assert.Equal(0, histogramPoint.BucketCounts[5]);

            List<MetricPoint> metricPointsCustom = new List<MetricPoint>();
            foreach (ref var mp in metricCustom.GetMetricPoints())
            {
                metricPointsCustom.Add(mp);
            }

            Assert.Single(metricPointsCustom);
            histogramPoint = metricPointsCustom[0];

            Assert.Equal(40, histogramPoint.DoubleValue);
            Assert.Equal(7, histogramPoint.LongValue);
            Assert.Equal(bounds.Length + 1, histogramPoint.BucketCounts.Length);
            Assert.Equal(5, histogramPoint.BucketCounts[0]);
            Assert.Equal(2, histogramPoint.BucketCounts[1]);
            Assert.Equal(0, histogramPoint.BucketCounts[2]);
        }
    }
}
