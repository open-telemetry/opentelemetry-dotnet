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
#pragma warning disable SA1000 // KeywordsMustBeSpacedCorrectly https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/3214
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
            foreach (ref var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            Assert.Single(metricPoints);
            var metricPoint = metricPoints[0];
            Assert.Equal(100, metricPoint.LongValue);
            Assert.NotNull(metricPoint.Keys);
            Assert.NotNull(metricPoint.Values);
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
            Assert.Equal(2, exportedItems.Count);
            var metric = exportedItems[0];
            Assert.Equal("myGauge", metric.Name);
            List<MetricPoint> metricPoints = new List<MetricPoint>();
            foreach (ref var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            Assert.Single(metricPoints);
            var metricPoint = metricPoints[0];
            Assert.Equal(100, metricPoint.LongValue);
            Assert.NotNull(metricPoint.Keys);
            Assert.NotNull(metricPoint.Values);

            metric = exportedItems[1];
            Assert.Equal("myBadGauge", metric.Name);
            metricPoints.Clear();
            foreach (ref var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            Assert.Empty(metricPoints);
        }

        [Theory]
        [InlineData(AggregationTemporality.Cumulative)]
        [InlineData(AggregationTemporality.Delta)]
        public void StreamNamesDuplicatesAreNotAllowedTest(AggregationTemporality temporality)
        {
            var metricItems = new List<Metric>();
            var metricExporter = new InMemoryExporter<Metric>(metricItems);

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = temporality,
            };
            using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.1.{temporality}");
            using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.2.{temporality}");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter1.Name)
                .AddMeter(meter2.Name)
                .AddReader(metricReader)
                .Build();

            // Expecting one metric stream.
            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            metricReader.Collect();
            Assert.Single(metricItems);

            // The following will be ignored as
            // metric of same name exists.
            // Metric stream will remain one.
            var anotherCounterSameName = meter1.CreateCounter<long>("name1");
            anotherCounterSameName.Add(10);
            metricItems.Clear();
            metricReader.Collect();
            Assert.Single(metricItems);

            // The following will also be ignored
            // as the name is same.
            // (the Meter name is not part of stream name)
            var anotherCounterSameNameDiffMeter = meter2.CreateCounter<long>("name1");
            anotherCounterSameNameDiffMeter.Add(10);
            metricItems.Clear();
            metricReader.Collect();
            Assert.Single(metricItems);
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
            var metricItems = new List<Metric>();
            var metricExporter = new InMemoryExporter<Metric>(metricItems);

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
            };

            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
            var counterLong = meter.CreateCounter<long>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(metricReader)
                .Build();

            counterLong.Add(10);
            counterLong.Add(10);
            metricReader.Collect();
            long sumReceived = GetLongSum(metricItems);
            Assert.Equal(20, sumReceived);

            metricItems.Clear();
            counterLong.Add(10);
            counterLong.Add(10);
            metricReader.Collect();
            sumReceived = GetLongSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(20, sumReceived);
            }
            else
            {
                Assert.Equal(40, sumReceived);
            }

            metricItems.Clear();
            metricReader.Collect();
            sumReceived = GetLongSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(0, sumReceived);
            }
            else
            {
                Assert.Equal(40, sumReceived);
            }

            metricItems.Clear();
            counterLong.Add(40);
            counterLong.Add(20);
            metricReader.Collect();
            sumReceived = GetLongSum(metricItems);
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
            var metricItems = new List<Metric>();
            var metricExporter = new InMemoryExporter<Metric>(metricItems);

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
            };

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
                .AddReader(metricReader)
                .Build();

            metricReader.Collect();
            long sumReceived = GetLongSum(metricItems);
            Assert.Equal(10, sumReceived);

            metricItems.Clear();
            metricReader.Collect();
            sumReceived = GetLongSum(metricItems);
            if (exportDelta)
            {
                Assert.Equal(10, sumReceived);
            }
            else
            {
                Assert.Equal(20, sumReceived);
            }

            metricItems.Clear();
            metricReader.Collect();
            sumReceived = GetLongSum(metricItems);
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
        public void TestMetricPointCap(AggregationTemporality temporality)
        {
            var metricItems = new List<Metric>();
            var metricExporter = new InMemoryExporter<Metric>(metricItems);

            int MetricPointCount()
            {
                var count = 0;

                foreach (var metric in metricItems)
                {
                    foreach (ref var metricPoint in metric.GetMetricPoints())
                    {
                        count++;
                    }
                }

                return count;
            }

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = temporality,
            };
            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}");
            var counterLong = meter.CreateCounter<long>("mycounterCapTest");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(metricReader)
                .Build();

            // Make one Add with no tags.
            // as currently we reserve 0th index
            // for no tag point!
            // This may be changed later.
            counterLong.Add(10);
            for (int i = 0; i < AggregatorStore.MaxMetricPoints + 1; i++)
            {
                counterLong.Add(10, new KeyValuePair<string, object>("key", "value" + i));
            }

            metricReader.Collect();
            Assert.Equal(AggregatorStore.MaxMetricPoints, MetricPointCount());

            metricItems.Clear();
            metricReader.Collect();
            Assert.Equal(AggregatorStore.MaxMetricPoints, MetricPointCount());

            // These updates would be dropped.
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueA"));
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueB"));
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueC"));
            metricItems.Clear();
            metricReader.Collect();
            Assert.Equal(AggregatorStore.MaxMetricPoints, MetricPointCount());
        }

        [Fact]
        public void MultithreadedLongCounterTest()
        {
            var metricItems = new List<Metric>();
            var metricExporter = new InMemoryExporter<Metric>(metricItems);

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = AggregationTemporality.Cumulative,
            };

            using var meter = new Meter(Utils.GetCurrentMethodName());
            var counterLong = meter.CreateCounter<long>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(metricReader)
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

            metricReader.Collect();

            var sumReceived = GetLongSum(metricItems);
            var expectedSum = deltaLongValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
            Assert.Equal(expectedSum, sumReceived);
        }

        [Fact]
        public void MultithreadedDoubleCounterTest()
        {
            var metricItems = new List<Metric>();
            var metricExporter = new InMemoryExporter<Metric>(metricItems);

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = AggregationTemporality.Cumulative,
            };

            using var meter = new Meter(Utils.GetCurrentMethodName());
            var counterDouble = meter.CreateCounter<double>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(metricReader)
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

            metricReader.Collect();

            var sumReceived = GetDoubleSum(metricItems);
            var expectedSum = deltaDoubleValueUpdatedByEachCall * numberOfMetricUpdateByEachThread * numberOfThreads;
            var difference = Math.Abs(sumReceived - expectedSum);
            Assert.True(difference <= 0.0001);
        }

        [Theory]
        [MemberData(nameof(MetricsTestData.InvalidInstrumentNames), MemberType = typeof(MetricsTestData))]
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
        [MemberData(nameof(MetricsTestData.ValidInstrumentNames), MemberType = typeof(MetricsTestData))]
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

        private static long GetLongSum(List<Metric> metrics)
        {
            long sum = 0;
            foreach (var metric in metrics)
            {
                foreach (ref var metricPoint in metric.GetMetricPoints())
                {
                    sum += metricPoint.LongValue;
                }
            }

            return sum;
        }

        private static double GetDoubleSum(List<Metric> metrics)
        {
            double sum = 0;
            foreach (var metric in metrics)
            {
                foreach (ref var metricPoint in metric.GetMetricPoints())
                {
                    sum += metricPoint.DoubleValue;
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
#pragma warning restore SA1000 // KeywordsMustBeSpacedCorrectly
}
