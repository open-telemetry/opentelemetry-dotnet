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
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            var counterLong = meter.CreateCounter<long>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = AggregationTemporality.Cumulative,
                })
                .Build();

            // setup args to threads.
            var mreToBlockUpdateThreads = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStarted = new ManualResetEvent(false);

            var argToThread = new UpdateThreadArguments<long>();
            argToThread.DeltaValueUpdatedByEachCall = deltaLongValueUpdatedByEachCall;
            argToThread.Counter = counterLong;
            argToThread.ThreadsStartedCount = 0;
            argToThread.MreToBlockUpdateThread = mreToBlockUpdateThreads;
            argToThread.MreToEnsureAllThreadsStart = mreToEnsureAllThreadsStarted;

            Thread[] t = new Thread[numberOfThreads];
            for (int i = 0; i < numberOfThreads; i++)
            {
                t[i] = new Thread(CounterUpdateThread<long>);
                t[i].Start(argToThread);
            }

            // Block until all threads started.
            mreToEnsureAllThreadsStarted.WaitOne();

            Stopwatch sw = Stopwatch.StartNew();

            // unblock all the threads.
            // (i.e let them start counter.Add)
            mreToBlockUpdateThreads.Set();

            for (int i = 0; i < numberOfThreads; i++)
            {
                // wait for all threads to complete
                t[i].Join();
            }

            var timeTakenInMilliseconds = sw.ElapsedMilliseconds;
            this.output.WriteLine($"Took {timeTakenInMilliseconds} msecs. Total threads: {numberOfThreads}, each thread doing {numberOfMetricUpdateByEachThread} recordings.");

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            var sumReceived = GetLongSum(exportedItems);
            var expectedSum = deltaLongValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
            Assert.Equal(expectedSum, sumReceived);
        }

        [Fact]
        public void MultithreadedDoubleCounterTest()
        {
            var exportedItems = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            var counterDouble = meter.CreateCounter<double>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                {
                    Temporality = AggregationTemporality.Cumulative,
                })
                .Build();

            // setup args to threads.
            var mreToBlockUpdateThreads = new ManualResetEvent(false);
            var mreToEnsureAllThreadsStarted = new ManualResetEvent(false);

            var argToThread = new UpdateThreadArguments<double>();
            argToThread.DeltaValueUpdatedByEachCall = deltaDoubleValueUpdatedByEachCall;
            argToThread.Counter = counterDouble;
            argToThread.ThreadsStartedCount = 0;
            argToThread.MreToBlockUpdateThread = mreToBlockUpdateThreads;
            argToThread.MreToEnsureAllThreadsStart = mreToEnsureAllThreadsStarted;

            Thread[] t = new Thread[numberOfThreads];
            for (int i = 0; i < numberOfThreads; i++)
            {
                t[i] = new Thread(CounterUpdateThread<double>);
                t[i].Start(argToThread);
            }

            // Block until all threads started.
            mreToEnsureAllThreadsStarted.WaitOne();

            Stopwatch sw = Stopwatch.StartNew();

            // unblock all the threads.
            // (i.e let them start counter.Add)
            mreToBlockUpdateThreads.Set();

            for (int i = 0; i < numberOfThreads; i++)
            {
                // wait for all threads to complete
                t[i].Join();
            }

            var timeTakenInMilliseconds = sw.ElapsedMilliseconds;
            this.output.WriteLine($"Took {timeTakenInMilliseconds} msecs. Total threads: {numberOfThreads}, each thread doing {numberOfMetricUpdateByEachThread} recordings.");

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            var sumReceived = GetDoubleSum(exportedItems);
            var expectedSum = deltaDoubleValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
            var difference = Math.Abs(sumReceived - expectedSum);
            Assert.True(difference <= 0.0001);
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

        private static void CounterUpdateThread<T>(object obj)
            where T : struct, IComparable
        {
            var arguments = obj as UpdateThreadArguments<T>;
            if (arguments == null)
            {
                throw new Exception("Invalid args");
            }

            var mre = arguments.MreToBlockUpdateThread;
            var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
            var counter = arguments.Counter;
            var valueToUpdate = arguments.DeltaValueUpdatedByEachCall;
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

        private class UpdateThreadArguments<T>
            where T : struct, IComparable
        {
            public ManualResetEvent MreToBlockUpdateThread;
            public ManualResetEvent MreToEnsureAllThreadsStart;
            public int ThreadsStartedCount;
            public Counter<T> Counter;
            public T DeltaValueUpdatedByEachCall;
        }
    }
}
