// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class MetricPointReclaimTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MeasurementsAreNotDropped(bool emitMetricWithNoDimensions, bool threadingDisabled)
    {
        using var threadingOverride = ThreadingHelper.BeginThreadingOverride(threadingDisabled);

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("MyFruitCounter");

        const int NumberOfUpdateThreads = 25;
        const int MaxNumberOfDistinctMetricPoints = 4000; // Default max MetricPoints * 2

        using var exporter = new CustomExporter(assertNoDroppedMeasurements: true);
        using var metricReader = new PeriodicExportingMetricReader(
            exporter,
            exportIntervalMilliseconds: 10)
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
                    var i = Interlocked.Increment(ref threadArguments!.Counter);
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

            for (var i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(EmitMetric);
                threads[i].Start(threadArgs);
            }

            for (var i = 0; i < threads.Length; i++)
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
            for (var i = 0; i < MaxMetricPointsPerMetricStream; i++)
            {
                counter.Add(100, new KeyValuePair<string, object?>("key", $"value{i}"));
            }

            Assert.True(meterProvider.ForceFlush());
            Assert.True(meterProvider.ForceFlush());

            exporter.Sum = 0;

            void EmitMetric()
            {
                var numberOfMeasurements = 0;
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

            for (var i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(EmitMetric);
                threads[i].Start();
            }

            for (var i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            Assert.True(meterProvider.ForceFlush());
        }

        Assert.Equal(sum, exporter.Sum);
    }

    // Regression test for a metric point reclaim data race where a measurement recorded
    // concurrently with a snapshot could be stranded on a metric point that then looked
    // "drained" and was reclaimed, permanently losing the value.
    //
    // The race is between AggregatorStore.SnapshotDeltaWithMetricPointReclaim (which clears a
    // point's CollectPending flag) and MetricPoint.Update/CompleteUpdate (which sets it). Before
    // the fix, the snapshot cleared the flag *after* reading the running value; on a weak memory
    // model (e.g. Arm) the update's CollectPending write could lose the race with the snapshot's
    // NoCollectPending write, leaving the running value unexported on a point that is then
    // reclaimed in the next collect cycle.
    //
    // This is a stress test: it asserts that the total exported value always equals the total
    // recorded value (no measurement is ever lost).
    [Fact]
    public void MeasurementsAreNotLostWhenReclaimRacesWithUpdates()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var counter = meter.CreateCounter<long>("MyFruitCounter");

        long recordedSum = 0;
        long exportedSum = 0;

        // A small limit relative to the number of distinct tag values forces continuous
        // reclaim of metric points, maximizing the window for the snapshot/update race.
        const int MaxMetricPointsPerMetricStream = 10;
        const int NumberOfUpdateThreads = 8;
        const int DistinctTagValues = 500;
        const int MeasurementsPerThread = 100_000;

        using var exporter = new SumCapturingExporter(value => Interlocked.Add(ref exportedSum, value));
        using var metricReader = new BaseExportingMetricReader(exporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetMaxMetricPointsPerMetricStream(MaxMetricPointsPerMetricStream)
            .AddReader(metricReader)
            .Build())
        {
            var stopCollecting = 0;

            // Collect as aggressively as possible to drive reclaim churn and interleave snapshots
            // with in-flight updates.
            var collector = new Thread(() =>
            {
                while (Volatile.Read(ref stopCollecting) == 0)
                {
                    metricReader.Collect();
                }
            });
            collector.Start();

            var threads = new Thread[NumberOfUpdateThreads];
            for (var t = 0; t < threads.Length; t++)
            {
                var seed = t + 1;
                threads[t] = new Thread(() =>
                {
                    var random = new Random(seed);
                    for (var i = 0; i < MeasurementsPerThread; i++)
                    {
#pragma warning disable CA5394 // Insecure randomness is fine for a stress test
                        long value = random.Next(1, 100);
                        var tagValue = random.Next(DistinctTagValues);
#pragma warning restore CA5394
                        counter.Add(value, new KeyValuePair<string, object?>("key", tagValue));
                        Interlocked.Add(ref recordedSum, value);
                    }
                });
                threads[t].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Volatile.Write(ref stopCollecting, 1);
            collector.Join();

            Assert.True(meterProvider.ForceFlush());
        }

        Assert.Equal(Interlocked.Read(ref recordedSum), Interlocked.Read(ref exportedSum));
    }

    private sealed class ThreadArguments
    {
        public int Counter;
    }

    private sealed class SumCapturingExporter : BaseExporter<Metric>
    {
        private readonly Action<long> onSum;

        public SumCapturingExporter(Action<long> onSum)
        {
            this.onSum = onSum;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                if (!metric.MetricType.IsSum())
                {
                    continue;
                }

                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    this.onSum(metricPoint.GetSumLong());
                }
            }

            return ExportResult.Success;
        }
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
