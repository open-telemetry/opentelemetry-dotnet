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
using System.Linq;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricViewTests
    {
        private const int MaxTimeToAllowForFlush = 10000;

        [Fact]
        public void ViewToRenameMetric()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("name1", "renamed")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            var counterLong = meter.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("renamed", metric.Name);
        }

        [Theory]
        [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
        public void AddViewWithInvalidNameThrowsArgumentException(string viewNewName)
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithInvalidNameThrowsArgumentException");

            var ex = Assert.Throws<ArgumentException>(() => Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddView("name1", viewNewName)
                .AddInMemoryExporter(exportedItems)
                .Build());

            Assert.Contains($"Custom view name {viewNewName} is invalid.", ex.Message);

            ex = Assert.Throws<ArgumentException>(() => Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddView("name1", new MetricStreamConfiguration() { Name = viewNewName })
                .AddInMemoryExporter(exportedItems)
                .Build());

            Assert.Contains($"Custom view name {viewNewName} is invalid.", ex.Message);
        }

        [Fact]
        public void AddViewWithNullMetricStreamConfigurationThrowsArgumentnullException()
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithInvalidNameThrowsArgumentException");

            Assert.Throws<ArgumentNullException>(() => Sdk.CreateMeterProviderBuilder()
               .AddMeter(meter1.Name)
               .AddView("name1", (MetricStreamConfiguration)null)
               .AddInMemoryExporter(exportedItems)
               .Build());
        }

        [Fact]
        public void AddViewWithNameThrowsInvalidArgumentExceptionWhenConflict()
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithGuaranteedConflictThrowsInvalidArgumentException");

            Assert.Throws<ArgumentException>(() => Sdk.CreateMeterProviderBuilder()
               .AddMeter(meter1.Name)
               .AddView("instrumenta.*", name: "newname")
               .AddInMemoryExporter(exportedItems)
               .Build());
        }

        [Fact]
        public void AddViewWithNameInMetricStreamConfigurationThrowsInvalidArgumentExceptionWhenConflict()
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithGuaranteedConflictThrowsInvalidArgumentException");

            Assert.Throws<ArgumentException>(() => Sdk.CreateMeterProviderBuilder()
               .AddMeter(meter1.Name)
               .AddView("instrumenta.*", new MetricStreamConfiguration() { Name = "newname" })
               .AddInMemoryExporter(exportedItems)
               .Build());
        }

        [Fact]
        public void AddViewWithExceptionInUserCallbackAppliedDefault()
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithExceptionInUserCallback");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
               .AddMeter(meter1.Name)
               .AddView((instrument) => { throw new Exception("bad"); })
               .AddInMemoryExporter(exportedItems)
               .Build();

            using (var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log))
            {
                var counter1 = meter1.CreateCounter<long>("counter1");
                counter1.Add(1);
                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 41));
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // Counter is still reported with default config
            // even if View is ignored due to View exception.
            Assert.Single(exportedItems);
        }

        [Fact]
        public void AddViewWithExceptionInUserCallbackNoDefault()
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithExceptionInUserCallback");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
               .AddMeter(meter1.Name)
               .AddView((instrument) => { throw new Exception("bad"); })
               .AddView("*", MetricStreamConfiguration.Drop)
               .AddInMemoryExporter(exportedItems)
               .Build();

            using (var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log))
            {
                var counter1 = meter1.CreateCounter<long>("counter1");
                counter1.Add(1);
                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 41));
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // Counter is not reported.
            // as the View is ignored due to View exception.
            // and Default is suppressed with * -> Drop
            Assert.Empty(exportedItems);
        }

        [Fact]
        public void AddViewsWithAndWithoutExceptionInUserCallback()
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithExceptionInUserCallback");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
               .AddMeter(meter1.Name)
               .AddView((instrument) => { throw new Exception("bad"); })
               .AddView((instrument) => { return new MetricStreamConfiguration() { Name = "newname" }; })
               .AddInMemoryExporter(exportedItems)
               .Build();

            using (var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log))
            {
                var counter1 = meter1.CreateCounter<long>("counter1");
                counter1.Add(1);
                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 41));
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // Counter is still reported with 2nd View
            // even if 1st View is ignored due to View exception.
            Assert.Single(exportedItems);
            Assert.Equal("newname", exportedItems[0].Name);
        }

        [Theory]
        [MemberData(nameof(MetricTestData.InvalidHistogramBoundaries), MemberType = typeof(MetricTestData))]
        public void AddViewWithInvalidHistogramBoundsThrowsArgumentException(double[] boundaries)
        {
            var ex = Assert.Throws<ArgumentException>(() => Sdk.CreateMeterProviderBuilder()
                .AddView("name1", new ExplicitBucketHistogramConfiguration { Boundaries = boundaries }));

            Assert.Contains("Histogram boundaries must be in ascending order with distinct values", ex.Message);
        }

        [Theory]
        [MemberData(nameof(MetricTestData.InvalidHistogramBoundaries), MemberType = typeof(MetricTestData))]
        public void AddViewWithInvalidHistogramBoundsIgnored(double[] boundaries)
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("AddViewWithInvalidHistogramBoundsIgnored");

            var counter1 = meter1.CreateCounter<long>("counter1");

            using (var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddView((instrument) =>
                {
                    return instrument.Name == counter1.Name
                        ? new ExplicitBucketHistogramConfiguration() { Boundaries = boundaries }
                        : null;
                })
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                counter1.Add(1);
            }

            // Counter is aggregated with default configuration
            // as the View config is ignored due to invalid histogram bounds.
            Assert.Single(exportedItems);
        }

        [Theory]
        [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
        public void ViewWithValidNameExported(string viewNewName)
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter("ViewWithInvalidNameIgnoredTest");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddView("name1", viewNewName)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // Expecting one metric stream.
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal(viewNewName, metric.Name);
        }

        [Fact]
        public void ViewToRenameMetricConditionally()
        {
            using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.1");
            using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.2");

            var exportedItems = new List<Metric>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddMeter(meter2.Name)
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

        [Theory]
        [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
        public void ViewWithInvalidNameIgnoredConditionally(string viewNewName)
        {
            using var meter1 = new Meter("ViewToRenameMetricConditionallyTest");
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)

                // since here it's a func, we can't validate the name right away
                // so the view is allowed to be added, but upon instrument creation it's going to be ignored.
                .AddView((instrument) =>
                {
                    if (instrument.Meter.Name.Equals(meter1.Name, StringComparison.OrdinalIgnoreCase)
                        && instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                    {
                        // invalid instrument name as per the spec
                        return new MetricStreamConfiguration() { Name = viewNewName, Description = "new description" };
                    }
                    else
                    {
                        return null;
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Because the MetricStreamName passed is invalid, the view is ignored,
            // and default aggregation is used.
            var counter1 = meter1.CreateCounter<long>("name1", "unit", "original_description");
            counter1.Add(10);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            Assert.Single(exportedItems);
        }

        [Theory]
        [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
        public void ViewWithValidNameConditionally(string viewNewName)
        {
            using var meter1 = new Meter("ViewToRenameMetricConditionallyTest");
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddView((instrument) =>
                {
                    if (instrument.Meter.Name.Equals(meter1.Name, StringComparison.OrdinalIgnoreCase)
                        && instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                    {
                        // invalid instrument name as per the spec
                        return new MetricStreamConfiguration() { Name = viewNewName, Description = "new description" };
                    }
                    else
                    {
                        return null;
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            var counter1 = meter1.CreateCounter<long>("name1", "unit", "original_description");
            counter1.Add(10);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // Expecting one metric stream.
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal(viewNewName, metric.Name);
        }

        [Fact]
        public void ViewWithNullCustomNameTakesInstrumentName()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter("ViewToRenameMetricConditionallyTest");

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView((instrument) =>
                {
                    if (instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                    {
                        // null View name
                        return new MetricStreamConfiguration() { };
                    }
                    else
                    {
                        return null;
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            // Since the View name was null, the instrument name was used instead
            var counter1 = meter.CreateCounter<long>("name1", "unit", "original_description");
            counter1.Add(10);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // Expecting one metric stream.
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal(counter1.Name, metric.Name);
        }

        [Fact]
        public void ViewToProduceMultipleStreamsFromInstrument()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("name1", "renamedStream1")
                .AddView("name1", "renamedStream2")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting two metric stream.
            var counterLong = meter.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            Assert.Equal("renamedStream1", exportedItems[0].Name);
            Assert.Equal("renamedStream2", exportedItems[1].Name);
        }

        [Fact]
        public void ViewToProduceMultipleStreamsWithDuplicatesFromInstrument()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("name1", "renamedStream1")
                .AddView("name1", "renamedStream2")
                .AddView("name1", "renamedStream2")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting two metric stream.
            // the .AddView("name1", "renamedStream2")
            // won't produce new Metric as the name
            // conflicts.
            var counterLong = meter.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            Assert.Equal("renamedStream1", exportedItems[0].Name);
            Assert.Equal("renamedStream2", exportedItems[1].Name);
        }

        [Fact]
        public void ViewWithHistogramConfigurationIgnoredWhenAppliedToNonHistogram()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("NotAHistogram", new ExplicitBucketHistogramConfiguration() { Name = "ImAHistogram" })
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter = meter.CreateCounter<long>("NotAHistogram");
            counter.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            Assert.Single(exportedItems);
            var metric = exportedItems[0];

            Assert.Equal("NotAHistogram", metric.Name);

            List<MetricPoint> metricPoints = new List<MetricPoint>();
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            Assert.Single(metricPoints);
            var metricPoint = metricPoints[0];
            Assert.Equal(10, metricPoint.GetSumLong());
        }

        [Fact]
        public void ViewToProduceCustomHistogramBound()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            var boundaries = new double[] { 10, 20 };
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("MyHistogram", new ExplicitBucketHistogramConfiguration() { Name = "MyHistogramDefaultBound" })
                .AddView("MyHistogram", new ExplicitBucketHistogramConfiguration() { Boundaries = boundaries })
                .AddInMemoryExporter(exportedItems)
                .Build();

            var histogram = meter.CreateHistogram<long>("MyHistogram");
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
            foreach (ref readonly var mp in metricDefault.GetMetricPoints())
            {
                metricPointsDefault.Add(mp);
            }

            Assert.Single(metricPointsDefault);
            var histogramPoint = metricPointsDefault[0];

            var count = histogramPoint.GetHistogramCount();
            var sum = histogramPoint.GetHistogramSum();

            Assert.Equal(40, sum);
            Assert.Equal(7, count);

            int index = 0;
            int actualCount = 0;
            var expectedBucketCounts = new long[] { 2, 1, 2, 2, 0, 0, 0, 0, 0, 0, 0 };
            foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
            {
                Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
                index++;
                actualCount++;
            }

            Assert.Equal(Metric.DefaultHistogramBounds.Length + 1, actualCount);

            List<MetricPoint> metricPointsCustom = new List<MetricPoint>();
            foreach (ref readonly var mp in metricCustom.GetMetricPoints())
            {
                metricPointsCustom.Add(mp);
            }

            Assert.Single(metricPointsCustom);
            histogramPoint = metricPointsCustom[0];

            count = histogramPoint.GetHistogramCount();
            sum = histogramPoint.GetHistogramSum();

            Assert.Equal(40, sum);
            Assert.Equal(7, count);

            index = 0;
            actualCount = 0;
            expectedBucketCounts = new long[] { 5, 2, 0 };
            foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
            {
                Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
                index++;
                actualCount++;
            }

            Assert.Equal(boundaries.Length + 1, actualCount);
        }

        [Fact]
        public void ViewToSelectTagKeys()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("FruitCounter", new MetricStreamConfiguration()
                {
                    TagKeys = new string[] { "name" },
                    Name = "NameOnly",
                })
                .AddView("FruitCounter", new MetricStreamConfiguration()
                {
                    TagKeys = new string[] { "size" },
                    Name = "SizeOnly",
                })
                .AddView("FruitCounter", new MetricStreamConfiguration()
                {
                    TagKeys = Array.Empty<string>(),
                    Name = "NoTags",
                })
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counter = meter.CreateCounter<long>("FruitCounter");
            counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "small"));
            counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "small"));

            counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "medium"));
            counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "medium"));

            counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "large"));
            counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "large"));

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(3, exportedItems.Count);
            var metric = exportedItems[0];
            Assert.Equal("NameOnly", metric.Name);
            List<MetricPoint> metricPoints = new List<MetricPoint>();
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            // Only one point expected "apple"
            Assert.Single(metricPoints);

            metric = exportedItems[1];
            Assert.Equal("SizeOnly", metric.Name);
            metricPoints.Clear();
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            // 3 points small,medium,large expected
            Assert.Equal(3, metricPoints.Count);

            metric = exportedItems[2];
            Assert.Equal("NoTags", metric.Name);
            metricPoints.Clear();
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            // Single point expected.
            Assert.Single(metricPoints);
        }

        [Fact]
        public void ViewToDropSingleInstrument()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("counterNotInteresting", MetricStreamConfiguration.Drop)
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            var counterInteresting = meter.CreateCounter<long>("counterInteresting");
            var counterNotInteresting = meter.CreateCounter<long>("counterNotInteresting");
            counterInteresting.Add(10);
            counterNotInteresting.Add(10);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("counterInteresting", metric.Name);
        }

        [Fact]
        public void ViewToDropSingleInstrumentObservableCounter()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("observableCounterNotInteresting", MetricStreamConfiguration.Drop)
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            meter.CreateObservableCounter("observableCounterNotInteresting", () => { return 10; }, "ms");
            meter.CreateObservableCounter("observableCounterInteresting", () => { return 10; }, "ms");

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("observableCounterInteresting", metric.Name);
        }

        [Fact]
        public void ViewToDropSingleInstrumentObservableGauge()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("observableGaugeNotInteresting", MetricStreamConfiguration.Drop)
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream.
            meter.CreateObservableGauge("observableGaugeNotInteresting", () => { return 10; }, "ms");
            meter.CreateObservableGauge("observableGaugeInteresting", () => { return 10; }, "ms");

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("observableGaugeInteresting", metric.Name);
        }

        [Fact]
        public void ViewToDropMultipleInstruments()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("server*", MetricStreamConfiguration.Drop)
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting two client metric streams as both server* are dropped.
            var serverRequests = meter.CreateCounter<long>("server.requests");
            var serverExceptions = meter.CreateCounter<long>("server.exceptions");
            var clientRequests = meter.CreateCounter<long>("client.requests");
            var clientExceptions = meter.CreateCounter<long>("client.exceptions");
            serverRequests.Add(10);
            serverExceptions.Add(10);
            clientRequests.Add(10);
            clientExceptions.Add(10);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            Assert.Equal("client.requests", exportedItems[0].Name);
            Assert.Equal("client.exceptions", exportedItems[1].Name);
        }

        [Fact]
        public void ViewToDropAndRetainInstrument()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddView("server.requests", MetricStreamConfiguration.Drop)
                .AddView("server.requests", "server.request_renamed")
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Expecting one metric stream even though a View is asking
            // to drop the instrument, because another View is matching
            // the instrument, which asks to aggregate with defaults
            // and a use a new name for the resulting metric.
            var serverRequests = meter.CreateCounter<long>("server.requests");
            serverRequests.Add(10);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            Assert.Equal("server.request_renamed", exportedItems[0].Name);
        }
    }
}
