// <copyright file="MetricAPITest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricApiTest
    {
        private const int MaxTimeToAllowForFlush = 10000;
        private static int numberOfThreads = Environment.ProcessorCount;
        private static long deltaLongValueUpdatedByEachCall = 10;
        private static double deltaDoubleValueUpdatedByEachCall = 11.987;
        private static int numberOfMetricUpdateByEachThread = 100000;
        private readonly ITestOutputHelper output;

        public MetricApiTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ObserverCallbackTest()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));
            meter.CreateObservableGauge("myGauge", () => measurement);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("myGauge", metric.Name);
            List<MetricPoint> metricPoints = new List<MetricPoint>();
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            Assert.Single(metricPoints);
            var metricPoint = metricPoints[0];
            Assert.Equal(100, metricPoint.GetGaugeLastValueLong());
            Assert.True(metricPoint.Tags.Count > 0);
        }

        [Fact]
        public void ObserverCallbackExceptionTest()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));
            meter.CreateObservableGauge("myGauge", () => measurement);
            meter.CreateObservableGauge<long>("myBadGauge", observeValues: () => throw new Exception("gauge read error"));

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("myGauge", metric.Name);
            List<MetricPoint> metricPoints = new List<MetricPoint>();
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            Assert.Single(metricPoints);
            var metricPoint = metricPoints[0];
            Assert.Equal(100, metricPoint.GetGaugeLastValueLong());
            Assert.True(metricPoint.Tags.Count > 0);
        }

        [Theory]
        [InlineData(AggregationTemporality.Cumulative, true)]
        [InlineData(AggregationTemporality.Cumulative, false)]
        [InlineData(AggregationTemporality.Delta, true)]
        [InlineData(AggregationTemporality.Delta, false)]
        public void DuplicateInstrumentNamesFromSameMeterAreNotAllowed(AggregationTemporality temporality, bool hasView)
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}");
            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = temporality,
                });

            if (hasView)
            {
                meterProviderBuilder.AddView("name1", new MetricStreamConfiguration() { Description = "description" });
            }

            using var meterProvider = meterProviderBuilder.Build();

            // Expecting one metric stream.
            var counterLong = meter.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);

            // The following will be ignored as
            // metric of same name exists.
            // Metric stream will remain one.
            var anotherCounterSameName = meter.CreateCounter<long>("name1");
            anotherCounterSameName.Add(10);
            counterLong.Add(10);
            exportedItems.Clear();
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
        }

        [Theory]
        [InlineData(AggregationTemporality.Cumulative, true)]
        [InlineData(AggregationTemporality.Cumulative, false)]
        [InlineData(AggregationTemporality.Delta, true)]
        [InlineData(AggregationTemporality.Delta, false)]
        public void DuplicateInstrumentNamesFromDifferentMetersAreAllowed(AggregationTemporality temporality, bool hasView)
        {
            var exportedItems = new List<Metric>();

            using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.1.{temporality}");
            using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.2.{temporality}");
            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddMeter(meter2.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = temporality,
                });

            if (hasView)
            {
                meterProviderBuilder.AddView("name1", new MetricStreamConfiguration() { Description = "description" });
            }

            using var meterProvider = meterProviderBuilder.Build();

            // Expecting one metric stream.
            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);

            // The following will not be ignored
            // as it is the same metric name but different meter.
            var anotherCounterSameNameDiffMeter = meter2.CreateCounter<long>("name1");
            anotherCounterSameNameDiffMeter.Add(10);
            counterLong.Add(10);
            exportedItems.Clear();
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MeterSourcesWildcardSupportMatchTest(bool hasView)
        {
            using var meter1 = new Meter("AbcCompany.XyzProduct.ComponentA");
            using var meter2 = new Meter("abcCompany.xYzProduct.componentC"); // Wildcard match is case insensitive.
            using var meter3 = new Meter("DefCompany.AbcProduct.ComponentC");
            using var meter4 = new Meter("DefCompany.XyzProduct.ComponentC"); // Wildcard match supports matching multiple patterns.
            using var meter5 = new Meter("GhiCompany.qweProduct.ComponentN");
            using var meter6 = new Meter("SomeCompany.SomeProduct.SomeComponent");

            var exportedItems = new List<Metric>();
            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter("AbcCompany.XyzProduct.*")
                .AddMeter("DefCompany.*.ComponentC")
                .AddMeter("GhiCompany.qweProduct.ComponentN") // Mixing of non-wildcard meter name and wildcard meter name.
                .AddInMemoryExporter(exportedItems);

            if (hasView)
            {
                meterProviderBuilder.AddView("myGauge1", "newName");
            }

            using var meterProvider = meterProviderBuilder.Build();

            var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));
            meter1.CreateObservableGauge("myGauge1", () => measurement);
            meter2.CreateObservableGauge("myGauge2", () => measurement);
            meter3.CreateObservableGauge("myGauge3", () => measurement);
            meter4.CreateObservableGauge("myGauge4", () => measurement);
            meter5.CreateObservableGauge("myGauge5", () => measurement);
            meter6.CreateObservableGauge("myGauge6", () => measurement);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            Assert.True(exportedItems.Count == 5); // "SomeCompany.SomeProduct.SomeComponent" will not be subscribed.

            if (hasView)
            {
                Assert.Equal("newName", exportedItems[0].Name);
            }
            else
            {
                Assert.Equal("myGauge1", exportedItems[0].Name);
            }

            Assert.Equal("myGauge2", exportedItems[1].Name);
            Assert.Equal("myGauge3", exportedItems[2].Name);
            Assert.Equal("myGauge4", exportedItems[3].Name);
            Assert.Equal("myGauge5", exportedItems[4].Name);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MeterSourcesWildcardSupportNegativeTestNoMeterAdded(bool hasView)
        {
            using var meter1 = new Meter($"AbcCompany.XyzProduct.ComponentA.{hasView}");
            using var meter2 = new Meter($"abcCompany.xYzProduct.componentC.{hasView}");

            var exportedItems = new List<Metric>();
            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddInMemoryExporter(exportedItems);

            if (hasView)
            {
                meterProviderBuilder.AddView("gauge1", "renamed");
            }

            using var meterProvider = meterProviderBuilder.Build();
            var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));

            meter1.CreateObservableGauge("myGauge1", () => measurement);
            meter2.CreateObservableGauge("myGauge2", () => measurement);

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.True(exportedItems.Count == 0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CounterAggregationTest(bool exportDelta)
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
            var counterLong = meter.CreateCounter<long>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
                })
                .Build();

            counterLong.Add(10);
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            long sumReceived = GetLongSum(exportedItems);
            Assert.Equal(20, sumReceived);

            exportedItems.Clear();
            counterLong.Add(10);
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            sumReceived = GetLongSum(exportedItems);
            if (exportDelta)
            {
                Assert.Equal(20, sumReceived);
            }
            else
            {
                Assert.Equal(40, sumReceived);
            }

            exportedItems.Clear();
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            sumReceived = GetLongSum(exportedItems);
            if (exportDelta)
            {
                Assert.Equal(0, sumReceived);
            }
            else
            {
                Assert.Equal(40, sumReceived);
            }

            exportedItems.Clear();
            counterLong.Add(40);
            counterLong.Add(20);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            sumReceived = GetLongSum(exportedItems);
            if (exportDelta)
            {
                Assert.Equal(60, sumReceived);
            }
            else
            {
                Assert.Equal(100, sumReceived);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ObservableCounterAggregationTest(bool exportDelta)
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
            int i = 1;
            var counterLong = meter.CreateObservableCounter(
                "observable-counter",
                () =>
                {
                    return new List<Measurement<long>>()
                    {
                        new Measurement<long>(i++ * 10),
                    };
                });

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
                })
                .Build();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            long sumReceived = GetLongSum(exportedItems);
            Assert.Equal(10, sumReceived);

            exportedItems.Clear();
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            sumReceived = GetLongSum(exportedItems);
            if (exportDelta)
            {
                Assert.Equal(10, sumReceived);
            }
            else
            {
                Assert.Equal(20, sumReceived);
            }

            exportedItems.Clear();
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            sumReceived = GetLongSum(exportedItems);
            if (exportDelta)
            {
                Assert.Equal(10, sumReceived);
            }
            else
            {
                Assert.Equal(30, sumReceived);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DimensionsAreOrderInsensitiveWithSortedKeysFirst(bool exportDelta)
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
            var counterLong = meter.CreateCounter<long>("Counter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
                })
                .Build();

            // Emit the first metric with the sorted order of tag keys
            counterLong.Add(5, new("Key1", "Value1"), new("Key2", "Value2"), new("Key3", "Value3"));
            counterLong.Add(10, new("Key1", "Value1"), new("Key3", "Value3"), new("Key2", "Value2"));
            counterLong.Add(10, new("Key2", "Value20"), new("Key1", "Value10"), new("Key3", "Value30"));

            // Emit a metric with different set of keys but the same set of values as one of the previous metric points
            counterLong.Add(25, new("Key4", "Value1"), new("Key5", "Value3"), new("Key6", "Value2"));
            counterLong.Add(25, new("Key4", "Value1"), new("Key6", "Value3"), new("Key5", "Value2"));

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            List<KeyValuePair<string, object>> expectedTagsForFirstMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key1", "Value1"),
                new("Key2", "Value2"),
                new("Key3", "Value3"),
            };

            List<KeyValuePair<string, object>> expectedTagsForSecondMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key1", "Value10"),
                new("Key2", "Value20"),
                new("Key3", "Value30"),
            };

            List<KeyValuePair<string, object>> expectedTagsForThirdMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key4", "Value1"),
                new("Key5", "Value3"),
                new("Key6", "Value2"),
            };

            List<KeyValuePair<string, object>> expectedTagsForFourthMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key4", "Value1"),
                new("Key5", "Value2"),
                new("Key6", "Value3"),
            };

            Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
            long sumReceived = GetLongSum(exportedItems);
            Assert.Equal(75, sumReceived);

            exportedItems.Clear();

            counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
            counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
            counterLong.Add(10, new("Key2", "Value2"), new("Key3", "Value3"), new("Key1", "Value1"));
            counterLong.Add(10, new("Key2", "Value20"), new("Key3", "Value30"), new("Key1", "Value10"));
            counterLong.Add(20, new("Key4", "Value1"), new("Key6", "Value2"), new("Key5", "Value3"));
            counterLong.Add(20, new("Key4", "Value1"), new("Key5", "Value2"), new("Key6", "Value3"));

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
            sumReceived = GetLongSum(exportedItems);
            if (exportDelta)
            {
                Assert.Equal(70, sumReceived);
            }
            else
            {
                Assert.Equal(145, sumReceived);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DimensionsAreOrderInsensitiveWithUnsortedKeysFirst(bool exportDelta)
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
            var counterLong = meter.CreateCounter<long>("Counter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
                })
                .Build();

            // Emit the first metric with the unsorted order of tag keys
            counterLong.Add(5, new("Key1", "Value1"), new("Key3", "Value3"), new("Key2", "Value2"));
            counterLong.Add(10, new("Key1", "Value1"), new("Key2", "Value2"), new("Key3", "Value3"));
            counterLong.Add(10, new("Key2", "Value20"), new("Key1", "Value10"), new("Key3", "Value30"));

            // Emit a metric with different set of keys but the same set of values as one of the previous metric points
            counterLong.Add(25, new("Key4", "Value1"), new("Key5", "Value3"), new("Key6", "Value2"));
            counterLong.Add(25, new("Key4", "Value1"), new("Key6", "Value3"), new("Key5", "Value2"));

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            List<KeyValuePair<string, object>> expectedTagsForFirstMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key1", "Value1"),
                new("Key2", "Value2"),
                new("Key3", "Value3"),
            };

            List<KeyValuePair<string, object>> expectedTagsForSecondMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key1", "Value10"),
                new("Key2", "Value20"),
                new("Key3", "Value30"),
            };

            List<KeyValuePair<string, object>> expectedTagsForThirdMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key4", "Value1"),
                new("Key5", "Value3"),
                new("Key6", "Value2"),
            };

            List<KeyValuePair<string, object>> expectedTagsForFourthMetricPoint = new List<KeyValuePair<string, object>>()
            {
                new("Key4", "Value1"),
                new("Key5", "Value2"),
                new("Key6", "Value3"),
            };

            Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
            long sumReceived = GetLongSum(exportedItems);
            Assert.Equal(75, sumReceived);

            exportedItems.Clear();

            counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
            counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
            counterLong.Add(10, new("Key2", "Value2"), new("Key3", "Value3"), new("Key1", "Value1"));
            counterLong.Add(10, new("Key2", "Value20"), new("Key3", "Value30"), new("Key1", "Value10"));
            counterLong.Add(20, new("Key4", "Value1"), new("Key6", "Value2"), new("Key5", "Value3"));
            counterLong.Add(20, new("Key4", "Value1"), new("Key5", "Value2"), new("Key6", "Value3"));

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
            CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
            sumReceived = GetLongSum(exportedItems);
            if (exportDelta)
            {
                Assert.Equal(70, sumReceived);
            }
            else
            {
                Assert.Equal(145, sumReceived);
            }
        }

        [Theory]
        [InlineData(AggregationTemporality.Cumulative)]
        [InlineData(AggregationTemporality.Delta)]
        public void TestInstrumentDisposal(AggregationTemporality temporality)
        {
            var exportedItems = new List<Metric>();

            var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}.1");
            var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}.2");
            var counter1 = meter1.CreateCounter<long>("counterFromMeter1");
            var counter2 = meter2.CreateCounter<long>("counterFromMeter2");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddMeter(meter2.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = temporality,
                })
                .Build();

            counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
            counter2.Add(10, new KeyValuePair<string, object>("key", "value"));

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            exportedItems.Clear();

            counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
            counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
            meter1.Dispose();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(2, exportedItems.Count);
            exportedItems.Clear();

            counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
            counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            exportedItems.Clear();

            counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
            counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
            meter2.Dispose();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Single(exportedItems);
            exportedItems.Clear();

            counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
            counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Empty(exportedItems);
        }

        [Theory]
        [InlineData(AggregationTemporality.Cumulative)]
        [InlineData(AggregationTemporality.Delta)]
        public void TestMetricPointCap(AggregationTemporality temporality)
        {
            var exportedItems = new List<Metric>();

            int MetricPointCount()
            {
                var count = 0;

                foreach (var metric in exportedItems)
                {
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        count++;
                    }
                }

                return count;
            }

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}");
            var counterLong = meter.CreateCounter<long>("mycounterCapTest");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = temporality,
                })
                .Build();

            // Make one Add with no tags.
            // as currently we reserve 0th index
            // for no tag point!
            // This may be changed later.
            counterLong.Add(10);
            for (int i = 0; i < MeterProviderBuilderBase.MaxMetricPointsPerMetricDefault + 1; i++)
            {
                counterLong.Add(10, new KeyValuePair<string, object>("key", "value" + i));
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(MeterProviderBuilderBase.MaxMetricPointsPerMetricDefault, MetricPointCount());

            exportedItems.Clear();
            counterLong.Add(10);
            for (int i = 0; i < MeterProviderBuilderBase.MaxMetricPointsPerMetricDefault + 1; i++)
            {
                counterLong.Add(10, new KeyValuePair<string, object>("key", "value" + i));
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(MeterProviderBuilderBase.MaxMetricPointsPerMetricDefault, MetricPointCount());

            counterLong.Add(10);
            for (int i = 0; i < MeterProviderBuilderBase.MaxMetricPointsPerMetricDefault + 1; i++)
            {
                counterLong.Add(10, new KeyValuePair<string, object>("key", "value" + i));
            }

            // These updates would be dropped.
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueA"));
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueB"));
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueC"));
            exportedItems.Clear();
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.Equal(MeterProviderBuilderBase.MaxMetricPointsPerMetricDefault, MetricPointCount());
        }

        [Fact]
        public void MultithreadedLongCounterTest()
        {
            this.MultithreadedCounterTest(deltaLongValueUpdatedByEachCall);
        }

        [Fact]
        public void MultithreadedDoubleCounterTest()
        {
            this.MultithreadedCounterTest(deltaDoubleValueUpdatedByEachCall);
        }

        [Fact]
        public void MultithreadedLongHistogramTest()
        {
            var expected = new long[11];
            for (var i = 0; i < expected.Length; i++)
            {
                expected[i] = numberOfThreads * numberOfMetricUpdateByEachThread;
            }

            // Metric.DefaultHistogramBounds: 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000
            var values = new long[] { -1, 1, 6, 20, 40, 60, 80, 200, 300, 600, 1001 };

            this.MultithreadedHistogramTest(expected, values);
        }

        [Fact]
        public void MultithreadedDoubleHistogramTest()
        {
            var expected = new long[11];
            for (var i = 0; i < expected.Length; i++)
            {
                expected[i] = numberOfThreads * numberOfMetricUpdateByEachThread;
            }

            // Metric.DefaultHistogramBounds: 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000
            var values = new double[] { -1.0, 1.0, 6.0, 20.0, 40.0, 60.0, 80.0, 200.0, 300.0, 600.0, 1001.0 };

            this.MultithreadedHistogramTest(expected, values);
        }

        [Theory]
        [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
        public void InstrumentWithInvalidNameIsIgnoredTest(string instrumentName)
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter("InstrumentWithInvalidNameIsIgnoredTest");

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counterLong = meter.CreateCounter<long>(instrumentName);
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // instrument should have been ignored
            // as its name does not comply with the specification
            Assert.Empty(exportedItems);
        }

        [Theory]
        [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
        public void InstrumentWithValidNameIsExportedTest(string name)
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter("InstrumentValidNameIsExportedTest");

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var counterLong = meter.CreateCounter<long>(name);
            counterLong.Add(10);
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            // Expecting one metric stream.
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal(name, metric.Name);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SetupSdkProviderWithNoReader(bool hasViews)
        {
            // This test ensures that MeterProviderSdk can be set up without any reader
            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{hasViews}");
            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name);

            if (hasViews)
            {
                meterProviderBuilder.AddView("counter", "renamedCounter");
            }

            using var meterProvider = meterProviderBuilder.Build();

            var counter = meter.CreateCounter<long>("counter");

            counter.Add(10, new KeyValuePair<string, object>("key", "value"));
        }

        private static long GetLongSum(List<Metric> metrics)
        {
            long sum = 0;
            foreach (var metric in metrics)
            {
                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    if (metric.MetricType.IsSum())
                    {
                        sum += metricPoint.GetSumLong();
                    }
                    else
                    {
                        sum += metricPoint.GetGaugeLastValueLong();
                    }
                }
            }

            return sum;
        }

        private static double GetDoubleSum(List<Metric> metrics)
        {
            double sum = 0;
            foreach (var metric in metrics)
            {
                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    if (metric.MetricType.IsSum())
                    {
                        sum += metricPoint.GetSumDouble();
                    }
                    else
                    {
                        sum += metricPoint.GetGaugeLastValueDouble();
                    }
                }
            }

            return sum;
        }

        private static int GetNumberOfMetricPoints(List<Metric> metrics)
        {
            int count = 0;
            foreach (var metric in metrics)
            {
                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    count++;
                }
            }

            return count;
        }

        // Provide tags input sorted by Key
        private static void CheckTagsForNthMetricPoint(List<Metric> metrics, List<KeyValuePair<string, object>> tags, int n)
        {
            var metric = metrics[0];
            var metricPointEnumerator = metric.GetMetricPoints().GetEnumerator();

            for (int i = 0; i < n; i++)
            {
                Assert.True(metricPointEnumerator.MoveNext());
            }

            int index = 0;
            var metricPoint = metricPointEnumerator.Current;
            foreach (var tag in metricPoint.Tags)
            {
                Assert.Equal(tags[index].Key, tag.Key);
                Assert.Equal(tags[index].Value, tag.Value);
                index++;
            }
        }

        private static void CounterUpdateThread<T>(object obj)
            where T : struct, IComparable
        {
            if (obj is not UpdateThreadArguments<T> arguments)
            {
                throw new Exception("Invalid args");
            }

            var mre = arguments.MreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
            var counter = arguments.Instrument as Counter<T>;
            var valueToUpdate = arguments.ValuesToRecord[0];
            if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == numberOfThreads)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < numberOfMetricUpdateByEachThread; i++)
            {
                counter.Add(valueToUpdate, new KeyValuePair<string, object>("verb", "GET"));
            }
        }

        private static void HistogramUpdateThread<T>(object obj)
            where T : struct, IComparable
        {
            if (obj is not UpdateThreadArguments<T> arguments)
            {
                throw new Exception("Invalid args");
            }

            var mre = arguments.MreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
            var histogram = arguments.Instrument as Histogram<T>;

            if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == numberOfThreads)
            {
                mreToEnsureAllThreadsStart.Set();
            }

            // Wait until signalled to start calling update on aggregator
            mre.WaitOne();

            for (int i = 0; i < numberOfMetricUpdateByEachThread; i++)
            {
                for (int j = 0; j < arguments.ValuesToRecord.Length; j++)
                {
                    histogram.Record(arguments.ValuesToRecord[j]);
                }
            }
        }

        private void MultithreadedCounterTest<T>(T deltaValueUpdatedByEachCall)
            where T : struct, IComparable
        {
            var metricItems = new List<Metric>();
            var metricReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(metricItems));

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{typeof(T).Name}.{deltaValueUpdatedByEachCall}");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(metricReader)
                .Build();

            var argToThread = new UpdateThreadArguments<T>
            {
                ValuesToRecord = new T[] { deltaValueUpdatedByEachCall },
                Instrument = meter.CreateCounter<T>("counter"),
                MreToBlockUpdateThread = new ManualResetEvent(false),
                MreToEnsureAllThreadsStart = new ManualResetEvent(false),
            };

            Thread[] t = new Thread[numberOfThreads];
            for (int i = 0; i < numberOfThreads; i++)
            {
                t[i] = new Thread(CounterUpdateThread<T>);
                t[i].Start(argToThread);
            }

            argToThread.MreToEnsureAllThreadsStart.WaitOne();
            Stopwatch sw = Stopwatch.StartNew();
            argToThread.MreToBlockUpdateThread.Set();

            for (int i = 0; i < numberOfThreads; i++)
            {
                t[i].Join();
            }

            this.output.WriteLine($"Took {sw.ElapsedMilliseconds} msecs. Total threads: {numberOfThreads}, each thread doing {numberOfMetricUpdateByEachThread} recordings.");

            metricReader.Collect();

            if (typeof(T) == typeof(long))
            {
                var sumReceived = GetLongSum(metricItems);
                var expectedSum = deltaLongValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
                Assert.Equal(expectedSum, sumReceived);
            }
            else if (typeof(T) == typeof(double))
            {
                var sumReceived = GetDoubleSum(metricItems);
                var expectedSum = deltaDoubleValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
                Assert.Equal(expectedSum, sumReceived, 2);
            }
        }

        private void MultithreadedHistogramTest<T>(long[] expected, T[] values)
            where T : struct, IComparable
        {
            var bucketCounts = new long[11];
            var metricReader = new BaseExportingMetricReader(new TestExporter<Metric>(batch =>
            {
                foreach (var metric in batch)
                {
                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        bucketCounts = metricPoint.GetHistogramBuckets().RunningBucketCounts;
                    }
                }
            }));

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{typeof(T).Name}");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(metricReader)
                .Build();

            var argsToThread = new UpdateThreadArguments<T>
            {
                Instrument = meter.CreateHistogram<T>("histogram"),
                MreToBlockUpdateThread = new ManualResetEvent(false),
                MreToEnsureAllThreadsStart = new ManualResetEvent(false),
                ValuesToRecord = values,
            };

            Thread[] t = new Thread[numberOfThreads];
            for (int i = 0; i < numberOfThreads; i++)
            {
                t[i] = new Thread(HistogramUpdateThread<T>);
                t[i].Start(argsToThread);
            }

            argsToThread.MreToEnsureAllThreadsStart.WaitOne();
            Stopwatch sw = Stopwatch.StartNew();
            argsToThread.MreToBlockUpdateThread.Set();

            for (int i = 0; i < numberOfThreads; i++)
            {
                t[i].Join();
            }

            this.output.WriteLine($"Took {sw.ElapsedMilliseconds} msecs. Total threads: {numberOfThreads}, each thread doing {numberOfMetricUpdateByEachThread * values.Length} recordings.");

            metricReader.Collect();

            Assert.Equal(expected, bucketCounts);
        }

        private class UpdateThreadArguments<T>
            where T : struct, IComparable
        {
            public ManualResetEvent MreToBlockUpdateThread;
            public ManualResetEvent MreToEnsureAllThreadsStart;
            public int ThreadsStartedCount;
            public Instrument<T> Instrument;
            public T[] ValuesToRecord;
        }
    }
}
