// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricPointReclaimTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MeasurementsAreNotDropped(bool emitMetricWithNoDimensions, bool useThreads)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("MyFruitCounter");

        const int NumberOfUpdateThreads = 25;
        const int MaxNumberOfDistinctMetricPoints = 4000; // Default max MetricPoints * 2

        using var exporter = new CustomExporter(assertNoDroppedMeasurements: true);
        using var metricReader = new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: 10, useThreads: useThreads)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var builder = Sdk.CreateMeterProviderBuilder()
                         .AddMeter(Utils.GetCurrentMethodName())
                         .AddReader(metricReader);

        using (var meterProvider = builder.Build())
        {
            void EmitMetric(object? obj)
            {
                var threadArguments = obj as ThreadArguments;
                var random = new Random();
                while (true)
                {
                    int i = Interlocked.Increment(ref threadArguments!.Counter);
                    if (i <= MaxNumberOfDistinctMetricPoints)
                    {
                        // Check for cases where a metric with no dimension is also emitted
                        if (emitMetricWithNoDimensions)
                        {
                            counter.Add(25);
                        }

                        // There are separate code paths for single dimension vs multiple dimensions
#pragma warning disable CA5394 // Do not use insecure randomness
                        if (random.Next(2) == 0)
#pragma warning restore CA5394 // Do not use insecure randomness
                        {
                            counter.Add(100, new KeyValuePair<string, object?>("key", $"value{i}"));
                        }
                        else
                        {
                            counter.Add(100, new KeyValuePair<string, object?>("key", $"value{i}"), new KeyValuePair<string, object?>("dimensionKey", "dimensionValue"));
                        }

                        Thread.Sleep(25);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            var threads = new Thread[NumberOfUpdateThreads];
            var threadArgs = new ThreadArguments();

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(EmitMetric);
                threads[i].Start(threadArgs);
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            Assert.True(meterProvider.ForceFlush());
        }

        long expectedSum =
            emitMetricWithNoDimensions ?
            MaxNumberOfDistinctMetricPoints * (100 + 25) :
            MaxNumberOfDistinctMetricPoints * 100;

        Assert.Equal(expectedSum, exporter.Sum);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MeasurementsAreAggregatedEvenAfterTheyAreDropped(bool emitMetricWithNoDimension)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("MyFruitCounter");

        long sum = 0;
        long[] measurementValues = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

        const int NumberOfUpdateThreads = 4;
        const int NumberOfMeasurementsPerThread = 10;

        using var exporter = new CustomExporter(assertNoDroppedMeasurements: false);
        using var metricReader = new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: 10)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        const int MaxMetricPointsPerMetricStream = 10;

        var builder = Sdk.CreateMeterProviderBuilder()
                         .AddMeter(Utils.GetCurrentMethodName())
                         .SetMaxMetricPointsPerMetricStream(MaxMetricPointsPerMetricStream)
                         .AddReader(metricReader);

        using (var meterProvider = builder.Build())
        {
            // Add distinct combinations of dimensions to surpass the max metric points limit of 10.
            // Note that one MetricPoint is reserved for zero tags and one MetricPoint is reserved for the overflow tag.
            // This would lead to dropping a few measurements. We want to make sure that they can still be
            // aggregated later on when there are free MetricPoints available.
            for (int i = 0; i < MaxMetricPointsPerMetricStream; i++)
            {
                counter.Add(100, new KeyValuePair<string, object?>("key", $"value{i}"));
            }

            Assert.True(meterProvider.ForceFlush());
            Assert.True(meterProvider.ForceFlush());

            exporter.Sum = 0;

            void EmitMetric()
            {
                int numberOfMeasurements = 0;
                var random = new Random();
                while (numberOfMeasurements < NumberOfMeasurementsPerThread)
                {
                    // Check for cases where a metric with no dimension is also emitted
                    if (emitMetricWithNoDimension)
                    {
                        counter.Add(25);
                        Interlocked.Add(ref sum, 25);
                    }

#pragma warning disable CA5394 // Do not use insecure randomness
                    var index = random.Next(measurementValues.Length);
#pragma warning restore CA5394 // Do not use insecure randomness
                    var measurement = measurementValues[index];
                    counter.Add(measurement, new KeyValuePair<string, object?>("key", $"value{index}"));
                    Interlocked.Add(ref sum, measurement);

                    numberOfMeasurements++;

                    Thread.Sleep(25);
                }
            }

            var threads = new Thread[NumberOfUpdateThreads];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(EmitMetric);
                threads[i].Start();
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            Assert.True(meterProvider.ForceFlush());
        }

        Assert.Equal(sum, exporter.Sum);
    }

    private sealed class ThreadArguments
    {
        public int Counter;
    }

    private sealed class CustomExporter : BaseExporter<Metric>
    {
        public long Sum;

        private readonly bool assertNoDroppedMeasurements;

        public CustomExporter(bool assertNoDroppedMeasurements)
        {
            this.assertNoDroppedMeasurements = assertNoDroppedMeasurements;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                var aggStore = metric.AggregatorStore;
                var metricPointLookupDictionary = aggStore.TagsToMetricPointIndexDictionaryDelta;
                var droppedMeasurements = aggStore.DroppedMeasurements;

                if (this.assertNoDroppedMeasurements)
                {
                    Assert.Equal(0, droppedMeasurements);
                }

                // This is to ensure that the lookup dictionary does not have unbounded growth
                Assert.NotNull(metricPointLookupDictionary);
                Assert.True(metricPointLookupDictionary.Count <= (MeterProviderBuilderSdk.DefaultCardinalityLimit * 2));

                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    // Access the tags to ensure that this does not throw any exception due to
                    // any erroneous thread interactions.
                    foreach (var tag in metricPoint.Tags)
                    {
                        _ = tag.Key;
                        _ = tag.Value;
                    }

                    if (metric.MetricType.IsSum())
                    {
                        Interlocked.Add(ref this.Sum, metricPoint.GetSumLong());
                    }
                }
            }

            return ExportResult.Success;
        }
    }
}
