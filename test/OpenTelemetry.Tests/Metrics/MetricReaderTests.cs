// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class MetricReaderTests
{
    [Theory]
    [InlineData("counter", typeof(long), AggregationTemporality.Delta)]
    [InlineData("counter", typeof(double), AggregationTemporality.Delta)]
    [InlineData("histogram", typeof(long), AggregationTemporality.Delta)]
    [InlineData("histogram", typeof(double), AggregationTemporality.Delta)]
    [InlineData("updowncounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("updowncounter", typeof(double), AggregationTemporality.Cumulative)]
    [InlineData("observablecounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("observablecounter", typeof(double), AggregationTemporality.Cumulative)]
    [InlineData("observableupdowncounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("observableupdowncounter", typeof(double), AggregationTemporality.Cumulative)]
    public void LowMemoryTemporality_UsesCorrectAggregationTemporality(string instrumentName, Type valueType, AggregationTemporality expectedTemporality)
    {
        var metrics = new List<Metric>();
        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.LowMemory;
            })
            .Build();

        switch (instrumentName)
        {
            case "counter":
                if (valueType == typeof(long))
                {
                    meter.CreateCounter<long>("test_counter").Add(1);
                }
                else
                {
                    meter.CreateCounter<double>("test_counter").Add(1);
                }

                break;

            case "histogram":
                if (valueType == typeof(long))
                {
                    meter.CreateHistogram<long>("test_histogram").Record(1);
                }
                else
                {
                    meter.CreateHistogram<double>("test_histogram").Record(1);
                }

                break;

            case "updowncounter":
                if (valueType == typeof(long))
                {
                    meter.CreateUpDownCounter<long>("test_updown").Add(1);
                }
                else
                {
                    meter.CreateUpDownCounter<double>("test_updown").Add(1);
                }

                break;

            case "observablecounter":
                if (valueType == typeof(long))
                {
                    meter.CreateObservableCounter("test_observable_counter", () => new Measurement<long>(1));
                }
                else
                {
                    meter.CreateObservableCounter("test_observable_counter", () => new Measurement<double>(1));
                }

                break;

            case "observableupdowncounter":
                if (valueType == typeof(long))
                {
                    meter.CreateObservableUpDownCounter("test_observable_updown", () => new Measurement<long>(1));
                }
                else
                {
                    meter.CreateObservableUpDownCounter("test_observable_updown", () => new Measurement<double>(1));
                }

                break;

            default:
                break;
        }

        provider.ForceFlush();

        var metric = Assert.Single(metrics);
        Assert.Equal(expectedTemporality, metric.Temporality);
    }

    [Fact]
    public async Task Collect_WhenConcurrentCallTimesOut_ReturnsFalseWithoutStartingAnotherCollection()
    {
        using var metricReader = new BlockingMetricReader();

        // Run each collection on a dedicated thread so the test does not depend on the
        // thread pool, which may be busy with other tests running at the same time.
        var firstCollectTask = RunOnDedicatedThread(() => metricReader.Collect(Timeout.Infinite));

        Assert.True(metricReader.CollectionStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        var secondCollectTask = RunOnDedicatedThread(() => metricReader.Collect(50));

        var firstCollectResult = false;

        try
        {
            Assert.False(await secondCollectTask);
            Assert.False(firstCollectTask.IsCompleted);
            Assert.Equal(1, metricReader.CollectCallCount);
        }
        finally
        {
            metricReader.AllowCollectionToFinish.Set();
            firstCollectResult = await firstCollectTask;
        }

        Assert.True(firstCollectResult);
    }

    [Theory]
    [InlineData(Timeout.Infinite, null, Timeout.Infinite)]
    [InlineData(5_000, null, 5_000)]
    [InlineData(Timeout.Infinite, Timeout.Infinite, Timeout.Infinite)]
    [InlineData(Timeout.Infinite, 0, Timeout.Infinite)]
    [InlineData(Timeout.Infinite, 1_234, 1_234)]
    [InlineData(5_000, 1_234, 1_234)]
    [InlineData(1_234, 5_000, 1_234)]
    public void Collect_PassesRemainingTimeoutToExporter(int timeoutMilliseconds, int? periodicExportTimeoutMilliseconds, int expectedTimeoutMilliseconds)
    {
        DateTime? exportDeadlineUtc = null;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ =>
        {
            exportDeadlineUtc ??= exporter.ExportDeadlineUtc;
            return ExportResult.Success;
        };

        // The export timeout of a periodic reader also applies to exports which
        // are not started by its worker, so the interval is long enough to never elapse.
#pragma warning disable CA2000 // Dispose objects before losing scope
        BaseExportingMetricReader reader = periodicExportTimeoutMilliseconds is { } periodicTimeout
            ? new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: int.MaxValue, exportTimeoutMilliseconds: periodicTimeout)
            : new BaseExportingMetricReader(exporter);
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(reader)
            .Build();

        meter.CreateCounter<long>("test_counter").Add(1);

        var before = DateTime.UtcNow;
        Assert.True(provider.ForceFlush(timeoutMilliseconds));
        var after = DateTime.UtcNow;

        Assert.NotNull(exportDeadlineUtc);

        if (expectedTimeoutMilliseconds == Timeout.Infinite)
        {
            Assert.Equal(DateTime.MaxValue, exportDeadlineUtc);
        }
        else
        {
            Assert.InRange(exportDeadlineUtc.Value, before, after.AddMilliseconds(expectedTimeoutMilliseconds));
        }

        // The deadline only applies to the export it was passed to
        Assert.Equal(DateTime.MaxValue, exporter.ExportDeadlineUtc);
    }

    [Theory]
    [InlineData(null, false, "a:3,b:2,c:4")]
    [InlineData(9, false, "a:3,b:2,c:4")]
    [InlineData(100, false, "a:3,b:2,c:4")]
    [InlineData(5, false, "a:3,b:2|c:4")]
    [InlineData(4, false, "a:3,b:1|b:1,c:3|c:1")]
    [InlineData(3, false, "a:3|b:2,c:1|c:3")]
    [InlineData(2, false, "a:2|a:1,b:1|b:1,c:1|c:2|c:1")]
    [InlineData(1, false, "a:1|a:1|a:1|b:1|b:1|c:1|c:1|c:1|c:1")]
    [InlineData(4, true, "a:3,b:1|b:1,c:3|c:1")]
    [InlineData(1, true, "a:1|a:1|a:1|b:1|b:1|c:1|c:1|c:1|c:1")]
    public void MaxExportBatchSize_SplitsMetricPointsIntoBatches(int? maxExportBatchSize, bool useDelta, string expectedBatches)
    {
        var batches = new List<string>();
        var exportedPoints = new List<string>();
        var exportedMetrics = new HashSet<Metric>();

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = batch =>
        {
            var metrics = new List<string>();

            foreach (var metric in batch)
            {
                var count = 0;

                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    foreach (var tag in metricPoint.Tags)
                    {
                        exportedPoints.Add($"{metric.Name}:{tag.Value}");
                    }

                    count++;
                }

                metrics.Add($"{metric.Name}:{count}");
                exportedMetrics.Add(metric);
            }

            batches.Add(string.Join(",", metrics));
            return ExportResult.Success;
        };

#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = maxExportBatchSize is { } size
            ? new PeriodicExportingMetricReader(exporter, int.MaxValue, PeriodicExportingMetricReader.DefaultExportTimeoutMilliseconds, size)
            : new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: int.MaxValue);
#pragma warning restore CA2000 // Dispose objects before losing scope

        reader.TemporalityPreference = useDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;

        Assert.Equal(maxExportBatchSize, reader.MaxExportBatchSize);

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(reader)
            .Build();

        var expectedPoints = new List<string>();

        foreach (var (name, count) in new[] { ("a", 3), ("b", 2), ("c", 4) })
        {
            var counter = meter.CreateCounter<long>(name);

            for (var i = 0; i < count; i++)
            {
                counter.Add(1, new KeyValuePair<string, object?>("key", i));
                expectedPoints.Add($"{name}:{i}");
            }
        }

        Assert.True(provider.ForceFlush());

        Assert.Equal(expectedBatches, string.Join("|", batches));

        // Each metric point is exported exactly once
        Assert.Equal(expectedPoints.OrderBy(p => p, StringComparer.Ordinal), exportedPoints.OrderBy(p => p, StringComparer.Ordinal));

        // All the metric points of a metric are available again once the batches have been exported
        foreach (var metric in exportedMetrics)
        {
            var count = 0;

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                count++;
            }

            Assert.Equal(metric.Name == "a" ? 3 : metric.Name == "b" ? 2 : 4, count);
        }
    }

    [Fact]
    public void MaxExportBatchSize_ExportsRemainingBatchesWhenABatchFails()
    {
        var exportCount = 0;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ => exportCount++ == 0 ? ExportResult.Failure : ExportResult.Success;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = new PeriodicExportingMetricReader(exporter, int.MaxValue, PeriodicExportingMetricReader.DefaultExportTimeoutMilliseconds, maxExportBatchSize: 1);
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(reader)
            .Build();

        var counter = meter.CreateCounter<long>("test_counter");

        for (var i = 0; i < 3; i++)
        {
            counter.Add(1, new KeyValuePair<string, object?>("key", i));
        }

        Assert.False(provider.ForceFlush());
        Assert.Equal(3, exportCount);
    }

    [Fact]
    public void MaxExportBatchSize_SkipsRemainingBatchesWhenTimeoutElapses()
    {
        var exportCount = 0;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ =>
        {
            if (exportCount++ == 0)
            {
                // Run past the deadline of the ForceFlush() call
                var deadline = exporter.ExportDeadlineUtc;

                while (DateTime.UtcNow <= deadline.AddMilliseconds(50))
                {
                    Thread.Sleep(10);
                }
            }

            return ExportResult.Success;
        };

#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = new PeriodicExportingMetricReader(exporter, int.MaxValue, PeriodicExportingMetricReader.DefaultExportTimeoutMilliseconds, maxExportBatchSize: 1);
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(reader)
            .Build();

        var counter = meter.CreateCounter<long>("test_counter");

        for (var i = 0; i < 3; i++)
        {
            counter.Add(1, new KeyValuePair<string, object?>("key", i));
        }

        Assert.False(provider.ForceFlush(timeoutMilliseconds: 500));
        Assert.Equal(1, exportCount);
    }

    [Fact]
    public void MaxExportBatchSize_ExportTimeoutAppliesToEachBatchExportedByWorker()
    {
        const int ExportTimeoutMilliseconds = 200;

        var exportCount = 0;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ =>
        {
            exportCount++;

            // Together the exports take longer than the export timeout
            Thread.Sleep(ExportTimeoutMilliseconds * 3 / 4);
            return ExportResult.Success;
        };

#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = new PeriodicExportingMetricReader(exporter, int.MaxValue, ExportTimeoutMilliseconds, maxExportBatchSize: 1);
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(reader)
            .Build();

        var counter = meter.CreateCounter<long>("test_counter");

        for (var i = 0; i < 3; i++)
        {
            counter.Add(1, new KeyValuePair<string, object?>("key", i));
        }

        // Shutting down the reader makes its worker collect the metrics
        Assert.True(reader.Shutdown());
        Assert.Equal(3, exportCount);
    }

    [Fact]
    public async Task MaxExportBatchSize_DoesNotExportBatchesAfterShutdownTimesOut()
    {
        using var exportStarted = new ManualResetEventSlim();
        using var allowExportToFinish = new ManualResetEventSlim();

        var exportCount = 0;
        var exportsAfterExporterShutdown = 0;
        var exporterShutdown = false;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ =>
        {
            if (Volatile.Read(ref exporterShutdown))
            {
                Interlocked.Increment(ref exportsAfterExporterShutdown);
            }

            if (Interlocked.Increment(ref exportCount) == 1)
            {
                exportStarted.Set();
                allowExportToFinish.Wait(TimeSpan.FromSeconds(30));
            }

            return ExportResult.Success;
        };
        exporter.OnShutdownFunc = _ =>
        {
            Volatile.Write(ref exporterShutdown, true);
            return true;
        };

#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = new PeriodicExportingMetricReader(exporter, int.MaxValue, PeriodicExportingMetricReader.DefaultExportTimeoutMilliseconds, maxExportBatchSize: 1);
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(reader)
            .Build();

        var counter = meter.CreateCounter<long>("test_counter");

        for (var i = 0; i < 3; i++)
        {
            counter.Add(1, new KeyValuePair<string, object?>("key", i));
        }

        // The collection has no overall timeout, so only the shutdown can stop its batches
        var flush = RunOnDedicatedThread(() => provider.ForceFlush());

        Assert.True(exportStarted.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        // The worker cannot collect while the first batch is being exported, so the shutdown times out
        Assert.False(reader.Shutdown(timeoutMilliseconds: 100));
        Assert.True(Volatile.Read(ref exporterShutdown));

        allowExportToFinish.Set();

        Assert.False(await flush);
        Assert.Equal(1, Volatile.Read(ref exportCount));
        Assert.Equal(0, Volatile.Read(ref exportsAfterExporterShutdown));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public void Shutdown_DoesNotExportWorkerCollectionAfterTimeout(int? maxExportBatchSize)
    {
        using var firstCollectionStarted = new ManualResetEventSlim();
        using var allowFirstCollectionToFinish = new ManualResetEventSlim();
        using var nextCollectionStarted = new ManualResetEventSlim();

        var callbackCount = 0;
        var exportsAfterExporterShutdown = 0;
        var exporterShutdown = false;

        using var meter = new Meter(Utils.GetCurrentMethodName());

        // The first collection by the worker is held in the callback until after the shutdown has timed out
        meter.CreateObservableGauge("test_gauge", () =>
        {
            if (Interlocked.Increment(ref callbackCount) == 1)
            {
                firstCollectionStarted.Set();
                allowFirstCollectionToFinish.Wait(TimeSpan.FromSeconds(30));
            }
            else
            {
                nextCollectionStarted.Set();
            }

            return 1;
        });

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ =>
        {
            if (Volatile.Read(ref exporterShutdown))
            {
                Interlocked.Increment(ref exportsAfterExporterShutdown);
            }

            return ExportResult.Success;
        };
        exporter.OnShutdownFunc = _ =>
        {
            Volatile.Write(ref exporterShutdown, true);
            return true;
        };

        const int ExportIntervalMilliseconds = 10;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = maxExportBatchSize is { } size
            ? new PeriodicExportingMetricReader(exporter, ExportIntervalMilliseconds, PeriodicExportingMetricReader.DefaultExportTimeoutMilliseconds, size)
            : new PeriodicExportingMetricReader(exporter, ExportIntervalMilliseconds);
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(reader)
            .Build();

        Assert.True(firstCollectionStarted.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        // The worker is held in the first collection, so the shutdown times out
        Assert.False(reader.Shutdown(timeoutMilliseconds: 100));
        Assert.True(Volatile.Read(ref exporterShutdown));

        allowFirstCollectionToFinish.Set();

        // The worker only starts its next collection once the first one has finished
        Assert.True(nextCollectionStarted.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.Equal(0, Volatile.Read(ref exportsAfterExporterShutdown));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxExportBatchSize_ThrowsWhenNotPositive(int maxExportBatchSize)
    {
        using var exporter = new DelegatingExporter<Metric>();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var reader = new PeriodicExportingMetricReader(exporter, int.MaxValue, PeriodicExportingMetricReader.DefaultExportTimeoutMilliseconds, maxExportBatchSize);
        });

        Assert.Equal("maxExportBatchSize", exception.ParamName);
    }

    private static Task<bool> RunOnDedicatedThread(Func<bool> collect)
        => Task.Factory.StartNew(collect, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private sealed class BlockingMetricReader : MetricReader
    {
        public ManualResetEventSlim CollectionStarted { get; } = new();

        public ManualResetEventSlim AllowCollectionToFinish { get; } = new();

        public int CollectCallCount { get; private set; }

        protected override bool OnCollect(int timeoutMilliseconds)
        {
            this.CollectCallCount++;
            this.CollectionStarted.Set();

            return this.AllowCollectionToFinish.Wait(TimeSpan.FromSeconds(5));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.CollectionStarted.Dispose();
                this.AllowCollectionToFinish.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
