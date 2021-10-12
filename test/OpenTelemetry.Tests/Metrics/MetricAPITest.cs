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
            using var meter = new Meter("ObserverCallbackErrorTest");
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
            using var meter = new Meter("ObserverCallbackErrorTest");
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
            int metricCount = 0;
            var metricExporter = new TestExporter<Metric>(ProcessExport);

            void ProcessExport(Batch<Metric> batch)
            {
                foreach (var metric in batch)
                {
                    metricCount++;
                }
            }

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = temporality,
            };
            using var meter1 = new Meter("TestDuplicateMetricName1");
            using var meter2 = new Meter("TestDuplicateMetricName2");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("TestDuplicateMetricName1")
                .AddMeter("TestDuplicateMetricName2")
                .AddReader(metricReader)
                .Build();

            // Expecting one metric stream.
            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            metricReader.Collect();
            Assert.Equal(1, metricCount);

            // The following will be ignored as
            // metric of same name exists.
            // Metric stream will remain one.
            var anotherCounterSameName = meter1.CreateCounter<long>("name1");
            anotherCounterSameName.Add(10);
            metricCount = 0;
            metricReader.Collect();
            Assert.Equal(1, metricCount);

            // The following will also be ignored
            // as the name is same.
            // (the Meter name is not part of stream name)
            var anotherCounterSameNameDiffMeter = meter2.CreateCounter<long>("name1");
            anotherCounterSameNameDiffMeter.Add(10);
            metricCount = 0;
            metricReader.Collect();
            Assert.Equal(1, metricCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MeterSourcesWildcardSupportMatchTest(bool hasView)
        {
            var meterNames = new[]
            {
                "AbcCompany.XyzProduct.ComponentA",
                "abcCompany.xYzProduct.componentC", // Wildcard match is case insensitive.
                "DefCompany.AbcProduct.ComponentC",
                "DefCompany.XyzProduct.ComponentC", // Wildcard match supports matching multiple patterns.
                "GhiCompany.qweProduct.ComponentN",
                "SomeCompany.SomeProduct.SomeComponent",
            };

            using var meter1 = new Meter(meterNames[0]);
            using var meter2 = new Meter(meterNames[1]);
            using var meter3 = new Meter(meterNames[2]);
            using var meter4 = new Meter(meterNames[3]);
            using var meter5 = new Meter(meterNames[4]);
            using var meter6 = new Meter(meterNames[5]);

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
            var meterNames = new[]
            {
                "AbcCompany.XyzProduct.ComponentA",
                "abcCompany.xYzProduct.componentC",
            };

            using var meter1 = new Meter(meterNames[0]);
            using var meter2 = new Meter(meterNames[1]);

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
            var metricExporter = new TestExporter<Metric>(ProcessExport);

            void ProcessExport(Batch<Metric> batch)
            {
                foreach (var metricItem in batch)
                {
                    metricItems.Add(metricItem);
                }
            }

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
            };

            using var meter = new Meter("TestMeter");
            var counterLong = meter.CreateCounter<long>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("TestMeter")
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
            var meterName = "TestMeter" + exportDelta;
            var metricItems = new List<Metric>();
            var metricExporter = new TestExporter<Metric>(ProcessExport);

            void ProcessExport(Batch<Metric> batch)
            {
                foreach (var metricItem in batch)
                {
                    metricItems.Add(metricItem);
                }
            }

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = exportDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative,
            };

            using var meter = new Meter(meterName);
            int i = 1;
            var counterLong = meter.CreateObservableCounter<long>(
            "observable-counter",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(i++ * 10),
                };
            });
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meterName)
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
            int metricPointCount = 0;
            var metricExporter = new TestExporter<Metric>(ProcessExport);

            void ProcessExport(Batch<Metric> batch)
            {
                foreach (var metric in batch)
                {
                    foreach (ref var metricPoint in metric.GetMetricPoints())
                    {
                        metricPointCount++;
                    }
                }
            }

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = temporality,
            };
            using var meter = new Meter("TestPointCapMeter");
            var counterLong = meter.CreateCounter<long>("mycounterCapTest");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("TestPointCapMeter")
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
            Assert.Equal(AggregatorStore.MaxMetricPoints, metricPointCount);

            metricPointCount = 0;
            metricReader.Collect();
            Assert.Equal(AggregatorStore.MaxMetricPoints, metricPointCount);

            // These updates would be dropped.
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueA"));
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueB"));
            counterLong.Add(10, new KeyValuePair<string, object>("key", "valueC"));
            metricPointCount = 0;
            metricReader.Collect();
            Assert.Equal(AggregatorStore.MaxMetricPoints, metricPointCount);
        }

        [Fact]
        public void MultithreadedLongCounterTest()
        {
            var metricItems = new List<Metric>();
            var metricExporter = new TestExporter<Metric>(ProcessExport);

            void ProcessExport(Batch<Metric> batch)
            {
                foreach (var metricItem in batch)
                {
                    metricItems.Add(metricItem);
                }
            }

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = AggregationTemporality.Cumulative,
            };

            using var meter = new Meter("TestLongCounterMeter");
            var counterLong = meter.CreateCounter<long>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("TestLongCounterMeter")
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
            var metricExporter = new TestExporter<Metric>(ProcessExport);

            void ProcessExport(Batch<Metric> batch)
            {
                foreach (var metricItem in batch)
                {
                    metricItems.Add(metricItem);
                }
            }

            var metricReader = new BaseExportingMetricReader(metricExporter)
            {
                PreferredAggregationTemporality = AggregationTemporality.Cumulative,
            };

            using var meter = new Meter("TestDoubleCounterMeter");
            var counterDouble = meter.CreateCounter<double>("mycounter");
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("TestDoubleCounterMeter")
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
