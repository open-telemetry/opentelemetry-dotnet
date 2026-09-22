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

    [Theory]
    [InlineData(1, false, MetricReaderTemporalityPreference.Delta)]
    [InlineData(1, true, MetricReaderTemporalityPreference.Delta)]
    [InlineData(2, false, MetricReaderTemporalityPreference.Delta)]
    [InlineData(2, true, MetricReaderTemporalityPreference.Delta)]
    [InlineData(1, false, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(1, true, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(2, false, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(2, true, MetricReaderTemporalityPreference.Cumulative)]
    public void ConcurrentMeasurementsForLastAvailableMetricPointAreNotSentToOverflow(
        int tagCount,
        bool useDouble,
        MetricReaderTemporalityPreference temporalityPreference)
    {
        const int CardinalityLimit = 2;

        using var lookupBlocked = new ManualResetEventSlim();
        using var continueLookup = new ManualResetEventSlim();
        using var meter = new Meter(Utils.GetCurrentMethodName());

        Action<double, KeyValuePair<string, object?>[]> recordMeasurement;
        if (useDouble)
        {
            var counter = meter.CreateCounter<double>("TestCounter");
            recordMeasurement = (value, tags) => counter.Add(value, tags);
        }
        else
        {
            var counter = meter.CreateCounter<long>("TestCounter");
            recordMeasurement = (value, tags) => counter.Add((long)value, tags);
        }

        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("TestCounter", new MetricStreamConfiguration
            {
                CardinalityLimit = CardinalityLimit,
                TagKeys = tagCount == 1 ? ["a"] : ["a", "b"],
            })
            .AddInMemoryExporter(
                exportedItems,
                options => options.TemporalityPreference = temporalityPreference)
            .Build();

        // The tag values deliberately collide. The blocked lookup captures the seed bucket,
        // then pauses while another thread publishes the target at the head of that bucket and
        // consumes the last MetricPoint. Resuming from the old bucket makes the first lookup miss.
        var seedTags = CreateCollidingTags(tagCount, "seed", blockLookup: false, lookupBlocked, continueLookup);
        var blockedTags = CreateCollidingTags(tagCount, "target", blockLookup: true, lookupBlocked, continueLookup);
        var creatorTags = CreateCollidingTags(tagCount, "target", blockLookup: false, lookupBlocked, continueLookup);
        var overflowTags = CreateCollidingTags(tagCount, "overflow", blockLookup: false, lookupBlocked, continueLookup);

        Assert.Equal(new Tags(creatorTags), new Tags(blockedTags));
        recordMeasurement(1, seedTags);

        Exception? blockedMeasurementException = null;
        var blockedMeasurement = new Thread(() =>
        {
            try
            {
                recordMeasurement(20, blockedTags);
            }
            catch (Exception ex)
            {
                blockedMeasurementException = ex;
            }
        });
        blockedMeasurement.Start();

        try
        {
            Assert.True(lookupBlocked.Wait(TimeSpan.FromSeconds(5)));
            recordMeasurement(10, creatorTags);

            if (temporalityPreference == MetricReaderTemporalityPreference.Cumulative)
            {
                // Advance the cumulative store's index to its full sentinel after the target
                // was published but before the stale target lookup resumes.
                recordMeasurement(40, overflowTags);
            }
        }
        finally
        {
            continueLookup.Set();
        }

        Assert.True(blockedMeasurement.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(blockedMeasurementException);

        Assert.True(meterProvider.ForceFlush());
        var metric = Assert.Single(exportedItems);
        Assert.Equal(
            temporalityPreference == MetricReaderTemporalityPreference.Cumulative ? 1 : 0,
            metric.AggregatorStore.DroppedMeasurements);

        var sums = new List<double>();
        double? overflowSum = null;
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            var sum = useDouble ? metricPoint.GetSumDouble() : metricPoint.GetSumLong();
            if (metricPoint.Tags.Count == 1 && metricPoint.Tags.KeyAndValues[0].Key == "otel.metric.overflow")
            {
                overflowSum = sum;
            }
            else
            {
                sums.Add(sum);
            }
        }

        sums.Sort();
        Assert.Equal([1, 30], sums);
        Assert.Equal(
            temporalityPreference == MetricReaderTemporalityPreference.Cumulative ? 40 : null,
            overflowSum);
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
    [SkipOnNetFrameworkArmFact]
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

    private static KeyValuePair<string, object?>[] CreateCollidingTags(
        int tagCount,
        string value,
        bool blockLookup,
        ManualResetEventSlim lookupBlocked,
        ManualResetEventSlim continueLookup)
    {
        var tagA = new KeyValuePair<string, object?>(
            "a",
            new CollidingTagValue(value, blockLookup, blockComparison: true, lookupBlocked, continueLookup));

        return tagCount == 1
            ? [tagA]
            :
            [
                new KeyValuePair<string, object?>(
                    "b",
                    new CollidingTagValue(value, blockLookup, blockComparison: false, lookupBlocked, continueLookup)),
                tagA,
            ];
    }

    private sealed class ThreadArguments
    {
        public int Counter;
    }

    private sealed class CollidingTagValue(
        string value,
        bool blockLookup,
        bool blockComparison,
        ManualResetEventSlim lookupBlocked,
        ManualResetEventSlim continueLookup)
    {
        private readonly string value = value;
        private readonly bool blockLookup = blockLookup;
        private readonly bool blockComparison = blockComparison;
        private readonly ManualResetEventSlim lookupBlocked = lookupBlocked;
        private readonly ManualResetEventSlim continueLookup = continueLookup;

        public override bool Equals(object? obj)
        {
            if (this.blockComparison &&
                string.Equals(this.value, "seed", StringComparison.Ordinal) &&
                obj is CollidingTagValue { blockLookup: true })
            {
                this.lookupBlocked.Set();
                this.continueLookup.Wait();
            }

            return obj is CollidingTagValue other &&
                string.Equals(this.value, other.value, StringComparison.Ordinal);
        }

        public override int GetHashCode() => 0;
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

    private sealed class SkipOnNetFrameworkArmFactAttribute : FactAttribute
    {
        public SkipOnNetFrameworkArmFactAttribute()
        {
#if NETFRAMEWORK
            foreach (var variable in new[] { "PROCESSOR_ARCHITEW6432", "PROCESSOR_ARCHITECTURE" })
            {
                var value = Environment.GetEnvironmentVariable(variable);

                if (value?.StartsWith("ARM", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.Skip = "Flaky on Windows 11 ARM: https://github.com/open-telemetry/opentelemetry-dotnet/pull/7470";
                    return;
                }
            }
#endif
        }
    }
}
