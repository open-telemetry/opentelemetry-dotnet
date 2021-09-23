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
    public class MetricApiTest
    {
        private static int numberOfThreads = Environment.ProcessorCount;
        private static long deltaLongValueUpdatedByEachCall = 10;
        private static double deltaDoubleValueUpdatedByEachCall = 11.987;
        private static int numberOfMetricUpdateByEachThread = 100000;
        private readonly ITestOutputHelper output;

        public MetricApiTest(ITestOutputHelper output)
        {
            this.output = output;
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
                .AddSource("TestMeter")
                .AddMetricReader(metricReader)
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
                .AddSource(meterName)
                .AddMetricReader(metricReader)
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
                .AddSource("TestPointCapMeter")
                .AddMetricReader(metricReader)
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
                .AddSource("TestLongCounterMeter")
                .AddMetricReader(metricReader)
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
                .AddSource("TestDoubleCounterMeter")
                .AddMetricReader(metricReader)
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
}
